using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.BettingEngine.DTOs;
using HorseRacing.Application.Features.BettingEngine.Interfaces;
using HorseRacing.Application.Features.FinancialRewards.Interfaces;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;
using Microsoft.Extensions.Configuration;

namespace HorseRacing.Application.Features.BettingEngine.Services;

public class BettingService : IBettingService
{
    private readonly IBetRepository _betRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IWalletTransactionRepository _transactionRepository;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;

    public BettingService(
        IBetRepository betRepository,
        IWalletRepository walletRepository,
        IWalletTransactionRepository transactionRepository,
        INotificationService notificationService,
        IConfiguration configuration)
    {
        _betRepository = betRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _notificationService = notificationService;
        _configuration = configuration;
    }

    public async Task<decimal> CalculateCurrentOddsAsync(long raceId, int horseId)
    {
        var bets = await _betRepository.GetByRaceIdAsync(raceId);
        var activeBets = bets.Where(b => b.Status != "Refunded").ToList();

        var totalRaceBets = activeBets.Sum(b => b.Amount);
        var totalHorseBets = activeBets.Where(b => b.HorseId == horseId).Sum(b => b.Amount);

        if (totalRaceBets == 0)
        {
            return 2.0m;
        }

        if (totalHorseBets == 0)
        {
            var tempOdds = (totalRaceBets * 0.9m) / 100m;
            return Math.Max(tempOdds, 3.5m);
        }

        var odds = (totalRaceBets * 0.9m) / totalHorseBets;
        return Math.Max(odds, 1.1m);
    }

    public async Task<BetTicketResponse> PlaceBetAsync(int userId, PlaceBetRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Bet amount must be greater than zero.");
        }

        var minBetAmount = _configuration.GetValue<decimal>("BettingSettings:MinimumAmount", 10000m);
        if (request.Amount < minBetAmount)
        {
            throw new ArgumentException($"Bet amount must be at least {minBetAmount:N0}.");
        }

        var entry = await _betRepository.GetRaceEntryByIdAsync(request.RaceEntryId);

        if (entry == null)
        {
            throw new ArgumentException($"Race entry with ID {request.RaceEntryId} not found.");
        }

        var race = entry.Race;
        if (race == null)
        {
            throw new InvalidOperationException("Race entry is not associated with a valid race.");
        }

        if (string.Equals(race.Status, "Finished", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(race.Status, "Running", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot place bet. Race status is '{race.Status}'.");
        }

        var existingBets = await _betRepository.GetByRaceIdAsync(race.RaceId);
        if (existingBets.Any(b => b.UserId == userId && b.Status == "Pending"))
        {
            throw new InvalidOperationException("You already have a pending bet for this race. Arbitrage betting is not allowed.");
        }

        var horse = entry.Registration?.Horse;
        if (horse == null)
        {
            throw new ArgumentException("Horse details not found for this entry.");
        }

        var invalidTournamentStatuses = new[] { "finished", "completed", "cancelled", "ended" };
        var tournamentStatus = race.Round?.Tournament?.Status?.ToLower() ?? "";
        if (invalidTournamentStatuses.Contains(tournamentStatus))
        {
            throw new ArgumentException("Cannot place bet because tournament has ended.");
        }

        var validRaceStatuses = new[] { "upcoming", "scheduled" };
        var raceStatus = race.Status?.ToLower() ?? "";
        if (!validRaceStatuses.Contains(raceStatus))
        {
            throw new ArgumentException("Betting is only allowed for upcoming or scheduled races.");
        }

        // Check if race results already exist
        var result = await _betRepository.GetRaceResultAsync(race.RaceId);
        if (result != null)
        {
            throw new ArgumentException("Cannot place bet because race results have already been published.");
        }

        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        if (wallet == null)
        {
            throw new InvalidOperationException("User wallet not found. Please deposit funds first.");
        }
        if (wallet.Balance < request.Amount)
        {
            throw new InvalidOperationException($"Insufficient wallet balance. Current: {wallet.Balance:N2}, Required: {request.Amount:N2}.");
        }

        // Odds must be taken from RaceEntry.CurrentOdds at the moment of betting
        var currentOdds = entry.CurrentOdds ?? 2.0m;

        wallet.Balance -= request.Amount;

        var transaction = new WalletTransaction
        {
            WalletId = wallet.WalletId,
            Amount = -request.Amount,
            Type = "BetPlaced",
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.AddAsync(transaction);

        var bet = new Bet
        {
            UserId = userId,
            RaceId = race.RaceId,
            HorseId = horse.HorseId,
            RaceEntryId = entry.RaceEntryId,
            Amount = request.Amount,
            Odds = currentOdds,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        await _betRepository.AddAsync(bet);

        // Save wallet changes
        try
        {
            await _walletRepository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            throw new InvalidOperationException("Your wallet balance was modified by another transaction. Please try again.");
        }

        await _notificationService.SendNotificationToUserAsync(
            userId,
            "Bet Placed Successfully",
            $"You successfully bet {request.Amount:N2}$ on horse '{horse.Name}' in race '{race.Name}' at odds {currentOdds:N2}.",
            "Bet",
            referenceId: (int)bet.Id,
            actionUrl: "/spectator/predictions"
        );

        return new BetTicketResponse
        {
            Id = bet.Id,
            UserId = bet.UserId,
            RaceId = bet.RaceId,
            RaceName = race.Name ?? "Unknown Race",
            HorseId = (int)bet.HorseId,
            HorseName = horse.Name,
            RaceEntryId = bet.RaceEntryId,
            Amount = bet.Amount,
            Odds = bet.Odds,
            Status = bet.Status,
            CreatedAt = bet.CreatedAt,
            PotentialPayout = bet.Amount * bet.Odds,
            ActualPayout = null,
            PayoutStatus = "Pending"
        };
    }

    public async Task<IEnumerable<BetTicketResponse>> GetMyBetsAsync(int userId)
    {
        var bets = await _betRepository.GetByUserIdAsync(userId);

        return bets.Select(b => new BetTicketResponse
        {
            Id = b.Id,
            UserId = b.UserId,
            RaceId = b.RaceId,
            RaceName = b.Race?.Name ?? "Unknown Race",
            HorseId = (int)b.HorseId,
            HorseName = b.Horse?.Name ?? "Unknown Horse",
            RaceEntryId = b.RaceEntryId,
            Amount = b.Amount,
            Odds = b.Odds,
            Status = b.Status,
            CreatedAt = b.CreatedAt,
            PotentialPayout = b.Amount * b.Odds,
            ActualPayout = b.Status == "Won" || b.Status == "PaidOut" ? b.Amount * b.Odds : null,
            PayoutStatus = b.Status switch
            {
                "Won" => "Won",
                "PaidOut" => "PaidOut",
                "Lost" => "Lost",
                _ => "Pending"
            }
        });
    }

    public async Task RecalculateRaceOddsAsync(long raceId)
    {
        var entries = (await _betRepository.GetRaceEntriesWithHorseAsync(raceId)).ToList();

        if (entries.Count == 0) return;

        var rawScores = new List<(RaceEntry entry, decimal speed, decimal jockey, decimal winRate)>();

        foreach (var entry in entries)
        {
            var horse = entry.Registration?.Horse;
            if (horse == null && entry.Registration != null && entry.Registration.HorseId > 0)
            {
                horse = await _betRepository.GetHorseByIdAsync(entry.Registration.HorseId);
            }
            if (horse == null) continue;

            // 1. Speed (stored in AverageTime / RecentAverageTime columns, representing AverageSpeed / RecentAverageSpeed)
            var avgSpeed = horse.AverageTime ?? 15.0m;
            if (avgSpeed <= 0) avgSpeed = 15.0m;

            var recentAvgSpeed = horse.RecentAverageTime ?? avgSpeed;
            if (recentAvgSpeed <= 0) recentAvgSpeed = avgSpeed;

            var combinedSpeed = 0.5m * avgSpeed + 0.5m * recentAvgSpeed;

            // 2. Jockey Ranking (RankingPoint in JockeyProfile)
            var jockeyRank = entry.JockeyProfile != null ? entry.JockeyProfile.RankingPoint : 100;
            if (jockeyRank <= 0) jockeyRank = 100;

            // 3. Win Rate (stored in WinRate, already smoothed by UpdateHorseStatsAsync or default)
            var winRate = horse.WinRate ?? 0.05m;
            if (winRate <= 0) winRate = 0.05m;
            if (winRate > 1) winRate = winRate / 100m;

            rawScores.Add((entry, combinedSpeed, (decimal)jockeyRank, winRate));
        }

        if (rawScores.Count == 0) return;

        var totalSpeed = rawScores.Sum(x => x.speed);
        var totalJockey = rawScores.Sum(x => x.jockey);
        var totalWinRate = rawScores.Sum(x => x.winRate);

        foreach (var item in rawScores)
        {
            // Normalize each factor to a percentage scale (relative share of the total)
            var relSpeed = totalSpeed > 0 ? item.speed / totalSpeed : 1.0m / rawScores.Count;
            var relJockey = totalJockey > 0 ? item.jockey / totalJockey : 1.0m / rawScores.Count;
            var relWinRate = totalWinRate > 0 ? item.winRate / totalWinRate : 1.0m / rawScores.Count;

            // Weighted probability: 50% Speed + 30% Jockey + 20% WinRate
            var winProbability = 0.5m * relSpeed + 0.3m * relJockey + 0.2m * relWinRate;
            var winPercentage = winProbability * 100m;

            // Odds = (1 / winProbability) * 0.9 (deducting 10% fee)
            var odds = winProbability > 0 ? 1.0m / winProbability : 1.0m / (1.0m / rawScores.Count);
            var finalOdds = Math.Max(odds * 0.9m, 1.05m);

            item.entry.WinningProbability = Math.Round(winPercentage, 2);
            item.entry.CurrentOdds = Math.Round(finalOdds, 2);
        }

        await _betRepository.SaveChangesAsync();
    }

    public async Task<RaceBettingInfoResponse> GetRaceBettingInfoAsync(int userId, long raceId)
    {
        var race = await _betRepository.GetRaceByIdAsync(raceId);

        if (race == null)
        {
            throw new KeyNotFoundException($"Race with ID {raceId} not found.");
        }

        // Dynamically calculate real odds using 3-factor formula before returning
        try
        {
            await RecalculateRaceOddsAsync(raceId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ODDS ERROR] Failed to recalculate odds: {ex.Message}");
        }

        var entries = (await _betRepository.GetRaceEntriesWithHorseAsync(raceId)).ToList();

        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        var balance = wallet?.Balance ?? 0;

        var hasResult = (await _betRepository.GetRaceResultAsync(raceId)) != null;

        var invalidTournamentStatuses = new[] { "finished", "completed", "cancelled", "ended" };
        var tournamentStatus = race.Round?.Tournament?.Status?.ToLower() ?? "";
        var isTournamentEnded = invalidTournamentStatuses.Contains(tournamentStatus);

        var validRaceStatuses = new[] { "upcoming", "scheduled" };
        var raceStatus = race.Status?.ToLower() ?? "";
        var isRaceActive = validRaceStatuses.Contains(raceStatus);

        bool canBet = isRaceActive && !isTournamentEnded && !hasResult;

        var entriesDto = entries.Select(e => new RaceEntryBettingDto
        {
            RaceEntryId = e.RaceEntryId,
            LaneNo = e.LaneNo,
            HorseId = e.Registration?.HorseId ?? 0,
            HorseName = e.Registration?.Horse?.Name ?? "Unknown Horse",
            JockeyName = e.JockeyProfile?.User?.FullName ?? "Unknown Jockey",
            AverageTime = e.Registration?.Horse?.AverageTime ?? 70m,
            RecentAverageTime = e.Registration?.Horse?.RecentAverageTime ?? (e.Registration?.Horse?.AverageTime ?? 70m),
            WinRate = e.Registration?.Horse?.WinRate ?? 0.05m,
            WinningProbability = e.WinningProbability ?? 0m,
            CurrentOdds = e.CurrentOdds ?? 2.0m
        }).ToList();

        return new RaceBettingInfoResponse
        {
            RaceId = race.RaceId,
            RaceName = race.Name ?? string.Empty,
            RaceStatus = race.Status ?? string.Empty,
            TournamentStatus = race.Round?.Tournament?.Status ?? string.Empty,
            CanBet = canBet,
            Entries = entriesDto
        };
    }
}
