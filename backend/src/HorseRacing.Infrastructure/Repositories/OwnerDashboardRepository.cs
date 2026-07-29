using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Repositories;

public class OwnerDashboardRepository : IOwnerDashboardRepository
{
    private readonly AppDbContext _context;

    public OwnerDashboardRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<OwnerDashboardDto> GetOwnerDashboardAsync(int ownerId)
    {
        // Horse count
        var horseCount = await _context.Horses
            .Where(h => h.OwnerId == ownerId)
            .CountAsync();

        // Get horse IDs for this owner
        var horseIds = await _context.Horses
            .Where(h => h.OwnerId == ownerId)
            .Select(h => h.HorseId)
            .ToListAsync();

        // Registration count
        var registrationCount = await _context.Registrations
            .Where(r => horseIds.Contains(r.HorseId))
            .CountAsync();

        // Active race count (races where owner's horses are entered and status is not Finished)
        var activeRaceCount = await _context.RaceEntries
            .Include(re => re.Race)
            .Include(re => re.Registration)
            .Where(re => horseIds.Contains(re.Registration.HorseId)
                && re.Race != null
                && (re.Race.Status == "Live" || re.Race.Status == "Running" || re.Race.Status == "InProgress"))
            .Select(re => re.RaceId)
            .Distinct()
            .CountAsync();

        // Upcoming race count
        var upcomingRaceCount = await _context.RaceEntries
            .Include(re => re.Race)
            .Include(re => re.Registration)
            .Where(re => horseIds.Contains(re.Registration.HorseId)
                && re.Race != null
                && re.Race.Status == "Scheduled")
            .Select(re => re.RaceId)
            .Distinct()
            .CountAsync();

        // Total prize amount from payouts
        var totalPrizeAmount = await _context.TournamentPrizePayouts
            .Where(tpp => tpp.UserId == ownerId)
            .SumAsync(tpp => (decimal?)tpp.Amount) ?? 0;

        return new OwnerDashboardDto
        {
            HorseCount = horseCount,
            RegistrationCount = registrationCount,
            ActiveRaceCount = activeRaceCount,
            UpcomingRaceCount = upcomingRaceCount,
            TotalPrizeAmount = totalPrizeAmount
        };
    }

    public async Task<List<OwnerResultDto>> GetOwnerResultsAsync(int ownerId)
    {
        var horseIds = await _context.Horses
            .Where(h => h.OwnerId == ownerId)
            .Select(h => h.HorseId)
            .ToListAsync();

        if (!horseIds.Any())
        {
            return new List<OwnerResultDto>();
        }

        var results = await _context.RaceEntries
            .Include(re => re.Race)
                .ThenInclude(r => r != null ? r.Round : null)
                    .ThenInclude(r0 => r0 != null ? r0.Tournament : null)
            .Include(re => re.Registration)
                .ThenInclude(reg => reg != null ? reg.Horse : null)
            .Where(re => re.Registration != null && horseIds.Contains(re.Registration.HorseId))
            .OrderByDescending(re => re.Race != null ? re.Race.RaceDate : DateTime.MinValue)
            .ToListAsync();

        var raceIds = results.Select(re => re.RaceId).Distinct().ToList();
        var winners = await _context.RaceResults
            .Where(rr => raceIds.Contains(rr.RaceId))
            .ToDictionaryAsync(rr => rr.RaceId, rr => rr.Winner);

        var tournamentIds = results.Where(re => re.Race?.Round?.TournamentId != null)
            .Select(re => re.Race!.Round!.TournamentId)
            .Distinct()
            .ToList();
            
        var prizes = await _context.Prizes
            .Where(p => tournamentIds.Contains(p.TournamentId))
            .ToListAsync();

        var ownerResults = results.Select(re => {
            var horseName = re.Registration?.Horse?.Name ?? "";
            var horseIdStr = re.Registration?.HorseId.ToString() ?? "";
            var raceStatus = re.Race?.Status ?? "Scheduled";
            
            int finishPosition = re.FinishPosition ?? 0;
            if (finishPosition == 0 && raceStatus.Equals("Finished", StringComparison.OrdinalIgnoreCase))
            {
                finishPosition = 2; // Default for finished
                if (winners.TryGetValue(re.RaceId, out var winner))
                {
                    if (winner.Equals(horseName, StringComparison.OrdinalIgnoreCase) || winner == horseIdStr)
                    {
                        finishPosition = 1;
                    }
                }
            }

            decimal prizeAmount = 0;
            if (raceStatus.Equals("Finished", StringComparison.OrdinalIgnoreCase))
            {
                if (re.Race?.Round?.RoundNumber == 2)
                {
                    var prize = prizes.FirstOrDefault(p => p.TournamentId == re.Race.Round.TournamentId && p.RankPosition == finishPosition);
                    if (prize != null)
                    {
                        prizeAmount = prize.Amount * (prize.OwnerPercentage / 100m);
                    }
                }
                else if (finishPosition == 1)
                {
                    // TODO: Revisit this legacy logic. We should rely on TournamentPrizePayouts instead.
                    // Fallback legacy support for pre-round winners showing a default win indicator
                    prizeAmount = 1000000;
                }
            }

            return new OwnerResultDto {
                RaceId = re.RaceId,
                RaceName = re.Race?.Name ?? "",
                TournamentName = re.Race?.Round?.Tournament?.Name ?? "",
                HorseName = horseName,
                FinishPosition = finishPosition,
                FinishTime = raceStatus.Equals("Finished", StringComparison.OrdinalIgnoreCase) && re.Race?.RaceDate != null
                    ? re.Race.RaceDate.AddMinutes(5).ToString("HH:mm:ss")
                    : "—",
                Point = raceStatus.Equals("Finished", StringComparison.OrdinalIgnoreCase)
                    ? (finishPosition == 1 ? 10 : 5)
                    : 0,
                PrizeAmount = prizeAmount,
                Status = raceStatus,
                LaneNo = re.LaneNo,
                RaceDate = re.Race != null ? re.Race.RaceDate : (DateTime?)null
            };
        }).ToList();

        return ownerResults;
    }
}
