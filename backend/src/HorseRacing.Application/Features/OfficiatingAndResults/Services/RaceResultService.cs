using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.OfficiatingAndResults.DTOs;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;
using HorseRacing.Application.Features.FinancialRewards.Interfaces;
using HorseRacing.Application.Features.BettingEngine.Interfaces;
using System.Linq;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Application.Features.TournamentAndRacing.Services;

namespace HorseRacing.Application.Features.OfficiatingAndResults.Services;

public class RaceResultService : IRaceResultService
{
    private readonly IResultRepository _repository;
    private readonly IBetPayoutService _betPayoutService;
    private readonly IPredictionService _predictionService;
    private readonly INotificationService _notificationService;
    private readonly IPrizePayoutService _prizePayoutService;
    private readonly ITournamentService _tournamentService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RaceResultService(
        IResultRepository repository,
        IBetPayoutService betPayoutService,
        IPredictionService predictionService,
        INotificationService notificationService,
        IPrizePayoutService prizePayoutService,
        ITournamentService tournamentService,
        IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _betPayoutService = betPayoutService;
        _predictionService = predictionService;
        _notificationService = notificationService;
        _prizePayoutService = prizePayoutService;
        _tournamentService = tournamentService;
        _httpContextAccessor = httpContextAccessor;
    }

    private static DateTime VietnamNow => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time");

    public async Task<RaceResultResponse> SubmitResultAsync(SubmitRaceResultRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // 0. Extract RefereeId from JWT Claims securely
        var nameIdentifier = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            
        if (string.IsNullOrEmpty(nameIdentifier) || !int.TryParse(nameIdentifier, out var userId))
        {
            throw new InvalidOperationException("Unauthorized: Unable to extract user identity.");
        }

        var refereeId = await _repository.GetRefereeIdByUserIdAsync(userId);
        if (refereeId == null)
        {
            throw new InvalidOperationException("User is not a registered referee.");
        }
        
        request.RefereeId = refereeId.Value;

        // 1. Validate race existence
        var race = await _repository.GetRaceByIdAsync(request.RaceId);
        if (race == null)
        {
            throw new KeyNotFoundException($"Race with ID {request.RaceId} was not found.");
        }
        if (race.RaceDate > VietnamNow)
            throw new InvalidOperationException("Race results cannot be submitted before the scheduled race time.");
        var submitAllowedStatuses = new[] { "Scheduled", "Active", "Live", "InProgress", "Running", "AwaitingResults" };
        if (!submitAllowedStatuses.Contains(race.Status, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Race results cannot be submitted while race status is '{race.Status}'.");

        // 2. Validate referee assignment (always mandatory)
        if (!request.RefereeId.HasValue)
        {
            throw new ArgumentException("RefereeId is required to submit race results.");
        }

        var refereeAssignment = await _repository.GetAssignmentAsync(request.RaceId, request.RefereeId.Value);
        if (refereeAssignment == null)
        {
            throw new InvalidOperationException("The referee is not assigned to this race.");
        }
        if (!string.Equals(refereeAssignment.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The referee assignment is no longer active.");

        // 3. Validate Winner parameter
        if (string.IsNullOrWhiteSpace(request.Winner))
        {
            throw new ArgumentException("Winner identifier cannot be empty.", nameof(request.Winner));
        }

        var horse = await _repository.GetHorseByIdOrNameAsync(request.Winner);
        if (horse == null)
        {
            throw new KeyNotFoundException($"Winner horse '{request.Winner}' was not found.");
        }

        // 4. Validate that the horse is entered in the race
        var entry = await _repository.GetRaceEntryByHorseIdAsync(request.RaceId, horse.HorseId);
        if (entry == null)
        {
            throw new ArgumentException($"Horse '{horse.Name}' is not entered in race with ID {request.RaceId}.");
        }

        // 4a. Validate that the winner horse is eligible to race (not sick/injured) and not in a non-participating status
        if (string.Equals(horse.HealthStatus, "Sick", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(horse.HealthStatus, "Injured", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Horse '{horse.Name}' is sick or injured and cannot be the winner.");
        }

        var invalidWinnerStatuses = new[] { "Withdrawn", "Scratch", "DNF", "Disqualified" };
        if (invalidWinnerStatuses.Any(s => string.Equals(entry.Status, s, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Horse '{horse.Name}' has race entry status '{entry.Status}' and cannot be the winner.");
        }

        // 5. Prevent duplicate result submission for the same race
        var existingResult = await _repository.GetResultByRaceIdAsync(request.RaceId);
        if (existingResult != null)
        {
            throw new InvalidOperationException("A result has already been submitted for this race.");
        }

        // Generate or save times and positions for all entries in that race if they exist
        var entries = (await _repository.GetRaceEntriesAsync(request.RaceId))?.ToList() ?? new List<RaceEntry>();
        if (entries.Any())
        {
            if (request.Entries != null && request.Entries.Any())
            {
                if (request.Entries.Any(item => item.RaceEntryId <= 0 || item.FinishPosition <= 0 || item.FinishTime <= 0))
                    throw new ArgumentException("Every result entry must have a valid entry ID, positive finish position, and positive finish time.");
                if (request.Entries.Select(item => item.RaceEntryId).Distinct().Count() != request.Entries.Count)
                    throw new ArgumentException("The submitted leaderboard contains duplicate race entries.");
                if (request.Entries.Select(item => item.FinishPosition).Distinct().Count() != request.Entries.Count)
                    throw new ArgumentException("Finish positions must be unique.");

                var eligibleEntries = entries.Where(item =>
                    item.Registration?.Horse != null &&
                    !new[] { "Sick", "Injured" }.Contains(item.Registration.Horse.HealthStatus, StringComparer.OrdinalIgnoreCase) &&
                    !invalidWinnerStatuses.Contains(item.Status, StringComparer.OrdinalIgnoreCase)).ToList();
                var submittedIds = request.Entries.Select(item => item.RaceEntryId).OrderBy(id => id).ToList();
                var eligibleIds = eligibleEntries.Select(item => item.RaceEntryId).OrderBy(id => id).ToList();
                if (!submittedIds.SequenceEqual(eligibleIds))
                    throw new ArgumentException("The leaderboard must contain every eligible horse in this race exactly once.");
                var winningInput = request.Entries.SingleOrDefault(item => item.FinishPosition == 1);
                if (winningInput == null || winningInput.RaceEntryId != entry.RaceEntryId)
                    throw new ArgumentException("Winner must match the race entry in finish position 1.");

                foreach (var manualEntry in request.Entries)
                {
                    var match = entries.FirstOrDefault(re => re.RaceEntryId == manualEntry.RaceEntryId);
                    if (match != null)
                    {
                        var isSickOrInjured = match.Registration?.Horse != null &&
                            (string.Equals(match.Registration.Horse.HealthStatus, "Sick", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(match.Registration.Horse.HealthStatus, "Injured", StringComparison.OrdinalIgnoreCase));

                        var hasNonParticipatingStatus = invalidWinnerStatuses.Any(s => string.Equals(match.Status, s, StringComparison.OrdinalIgnoreCase));

                        if (isSickOrInjured || hasNonParticipatingStatus)
                        {
                            if ((manualEntry.FinishPosition > 0) || (manualEntry.FinishTime > 0))
                            {
                                throw new ArgumentException($"Horse '{match.Registration?.Horse?.Name}' is sick, injured, or withdrawn/disqualified/DNF ({match.Status}) and cannot have a finish position or time.");
                            }
                            match.FinishPosition = null;
                            match.FinishTime = null;
                        }
                        else
                        {
                            match.FinishPosition = manualEntry.FinishPosition;
                            match.FinishTime = manualEntry.FinishTime;
                            match.Status = "Finished";
                        }
                    }
                }
                await _repository.SaveChangesAsync();
            }
            else
            {
                var winnerEntry = entries.FirstOrDefault(re => re.Registration?.HorseId == horse.HorseId);
                if (winnerEntry == null)
                {
                    throw new ArgumentException($"Horse '{horse.Name}' is not entered in race with ID {request.RaceId}.");
                }

                var random = new Random();
                decimal winnerTime = Math.Round(55m + (decimal)random.NextDouble() * 10m, 2);

                winnerEntry.FinishPosition = 1;
                winnerEntry.FinishTime = winnerTime;
                winnerEntry.Status = "Finished";

                int position = 2;
                foreach (var entryItem in entries.Where(re => re.RaceEntryId != winnerEntry.RaceEntryId))
                {
                    var isSickOrInjured = entryItem.Registration?.Horse != null &&
                        (string.Equals(entryItem.Registration.Horse.HealthStatus, "Sick", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(entryItem.Registration.Horse.HealthStatus, "Injured", StringComparison.OrdinalIgnoreCase));

                    var hasNonParticipatingStatus = invalidWinnerStatuses.Any(s => string.Equals(entryItem.Status, s, StringComparison.OrdinalIgnoreCase));

                    if (isSickOrInjured || hasNonParticipatingStatus)
                    {
                        entryItem.FinishPosition = null;
                        entryItem.FinishTime = null;
                    }
                    else
                    {
                        entryItem.FinishPosition = position++;
                        entryItem.FinishTime = Math.Round(winnerTime + (decimal)(random.NextDouble() * 3.0 + 0.5), 2);
                        entryItem.Status = "Finished";
                    }
                }
                await _repository.SaveChangesAsync();
            }
        }

        // 6. Save result (using only actual properties from DB/entity)
        var result = new RaceResult
        {
            RaceId = request.RaceId,
            Winner = request.Winner
        };

        race.Status = "Completed";

        await _repository.AddResultAsync(result);
        await _repository.SaveChangesAsync();

        // Dispatch notifications
        try
        {
            // 1. Notify assigned referees
            var assignments = await _repository.GetAssignmentsForRaceAsync(race.RaceId);
            var tournamentName = race.Round?.Tournament?.Name ?? "Tournament";
            foreach (var assignment in assignments)
            {
                if (assignment.RefereeProfile != null)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        assignment.RefereeProfile.UserId,
                        "Assigned Tournament Has Ended",
                        $"Tournament '{tournamentName}', which you were assigned to officiate, has ended. Please submit all violation reports and race results for Admin review.",
                        "System",
                        referenceId: (int)race.RaceId,
                        actionUrl: "/referee/schedule"
                    );
                }
            }

            // 2. Notify admins
            var adminIds = await _repository.GetAdminUserIdsAsync();
            foreach (var adminId in adminIds)
            {
                await _notificationService.SendNotificationToUserAsync(
                    adminId,
                    "Race Results Submitted",
                    $"The results for race '{race.Name}' have been submitted and are pending review.",
                    "Race",
                    referenceId: (int)race.RaceId,
                    actionUrl: "/admin/results"
                );
            }

            // 3. Broadcast to all users
            await _notificationService.BroadcastNotificationAsync(
                "Race Completed",
                $"Race '{race.Name}' has completed. Results are pending review.",
                "Race",
                referenceId: (int)race.RaceId,
                actionUrl: "/spectator/live"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NOTIFICATION ERROR] Failed to dispatch notifications on race completion: {ex.Message}");
        }

        // Auto-trigger Final Race generation if all Pre-round races are completed
        await TryAutoGenerateFinalRaceAsync(race);

        return new RaceResultResponse
        {
            Id = result.Id,
            RaceId = result.RaceId,
            RaceName = race.Name ?? string.Empty,
            Winner = result.Winner,
            RaceEntryId = entry.RaceEntryId,
            HorseId = horse.HorseId,
            HorseName = horse.Name,
            JockeyId = entry.JockeyId,
            JockeyName = entry.JockeyProfile?.User?.FullName ?? string.Empty,
            Status = race.Status,
            ResultRecordedAt = result.ResultRecordedAt,
            CreatedAt = result.CreatedAt
        };
    }

    public async Task<RaceResultResponse> PublishResultAsync(long raceId)
    {
        // 1. Validate race existence
        var race = await _repository.GetRaceByIdAsync(raceId);
        if (race == null)
        {
            throw new KeyNotFoundException($"Race with ID {raceId} was not found.");
        }

        // 2. Validate result presence
        var result = await _repository.GetResultByRaceIdAsync(raceId);
        if (result == null || string.IsNullOrWhiteSpace(result.Winner))
        {
            throw new InvalidOperationException($"No results found for race with ID {raceId}. Cannot publish.");
        }

        // 3. Update race status to "Finished"
        bool wasFinished = race.Status.Equals("Finished", StringComparison.OrdinalIgnoreCase);
        race.Status = "Finished";

        if (!wasFinished)
        {
            // Fetch all entries to update horse stats
            var entries = (await _repository.GetRaceEntriesAsync(raceId))?.ToList() ?? new List<RaceEntry>();

            foreach (var entryItem in entries)
            {
                var horseId = entryItem.Registration?.HorseId;
                if (horseId == null) continue;

                await _repository.UpdateHorseStatsAsync(horseId.Value);
            }
        }
        await _repository.SaveChangesAsync();

        if (!wasFinished)
        {
            try
            {
                await _betPayoutService.ProcessPayoutAsync(raceId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BetPayout Error] Failed to process auto payout for race {raceId}: {ex.Message}");
            }

            try
            {
                await _predictionService.EvaluatePredictionsAsync(raceId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Prediction Error] Failed to evaluate predictions for race {raceId}: {ex.Message}");
            }

            // Tournament prizes are paid only when Admin explicitly completes the
            // tournament. Paying here hid failures and could debit a different Admin.
        }

        // 4. Resolve winner details
        var horse = await _repository.GetHorseByIdOrNameAsync(result.Winner);
        RaceEntry? entry = null;
        if (horse != null)
        {
            entry = await _repository.GetRaceEntryByHorseIdAsync(raceId, horse.HorseId);
        }

        try
        {
            await _notificationService.BroadcastNotificationAsync(
                "Race Results Published",
                $"The results for race '{race.Name}' have been published. Click to view details.",
                "Race",
                referenceId: (int)race.RaceId,
                actionUrl: $"/spectator/races/{race.RaceId}"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Notification Error] Failed to broadcast race result publication: {ex.Message}");
        }

        try
        {
            var refereeAssignments = await _repository.GetAssignmentsForRaceAsync(race.RaceId);
            foreach (var assignment in refereeAssignments.Where(a => a.RefereeProfile != null))
            {
                await _notificationService.SendNotificationToUserAsync(
                    assignment.RefereeProfile!.UserId,
                    "Race results approved",
                    $"Admin approved and published the results for race '{race.Name}'.",
                    "Result",
                    referenceId: (int)race.RaceId,
                    actionUrl: "/referee/confirm-results");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Notification Error] Failed to notify referees about result publication: {ex.Message}");
        }

        return new RaceResultResponse
        {
            Id = result.Id,
            RaceId = result.RaceId,
            RaceName = race.Name ?? string.Empty,
            Winner = result.Winner,
            RaceEntryId = entry?.RaceEntryId,
            HorseId = horse?.HorseId,
            HorseName = horse?.Name,
            JockeyId = entry?.JockeyId,
            JockeyName = entry?.JockeyProfile?.User?.FullName ?? string.Empty,
            Status = race.Status,
            ResultRecordedAt = result.ResultRecordedAt,
            CreatedAt = result.CreatedAt
        };
    }

    public async Task<List<RaceResultResponse>?> GetResultsByRaceIdAsync(long raceId)
    {
        var race = await _repository.GetRaceByIdAsync(raceId);
        if (race == null)
        {
            return null;
        }

        var result = await _repository.GetResultByRaceIdAsync(raceId);
        if (result == null)
        {
            return new List<RaceResultResponse>();
        }

        var horse = await _repository.GetHorseByIdOrNameAsync(result.Winner);
        RaceEntry? entry = null;
        if (horse != null)
        {
            entry = await _repository.GetRaceEntryByHorseIdAsync(raceId, horse.HorseId);
        }

        return new List<RaceResultResponse>
        {
            new RaceResultResponse
            {
                Id = result.Id,
                RaceId = result.RaceId,
                RaceName = race.Name ?? string.Empty,
                Winner = result.Winner,
                RaceEntryId = entry?.RaceEntryId,
                HorseId = horse?.HorseId,
                HorseName = horse?.Name,
                JockeyId = entry?.JockeyId,
                JockeyName = entry?.JockeyProfile?.User?.FullName ?? string.Empty,
                Status = race.Status,
                ResultRecordedAt = result.ResultRecordedAt,
                CreatedAt = result.CreatedAt
            }
        };
    }

    public async Task<List<RaceResultResponse>?> GetPublicResultsByRaceIdAsync(long raceId)
    {
        var race = await _repository.GetRaceByIdAsync(raceId);
        if (race == null)
        {
            return null;
        }

        // Public should only see published results (Status == "Finished")
        if (race.Status != "Finished")
        {
            return new List<RaceResultResponse>();
        }

        var result = await _repository.GetResultByRaceIdAsync(raceId);
        if (result == null)
        {
            return new List<RaceResultResponse>();
        }

        var horse = await _repository.GetHorseByIdOrNameAsync(result.Winner);
        RaceEntry? entry = null;
        if (horse != null)
        {
            entry = await _repository.GetRaceEntryByHorseIdAsync(raceId, horse.HorseId);
        }

        return new List<RaceResultResponse>
        {
            new RaceResultResponse
            {
                Id = result.Id,
                RaceId = result.RaceId,
                RaceName = race.Name ?? string.Empty,
                Winner = result.Winner,
                RaceEntryId = entry?.RaceEntryId,
                HorseId = horse?.HorseId,
                HorseName = horse?.Name,
                JockeyId = entry?.JockeyId,
                JockeyName = entry?.JockeyProfile?.User?.FullName ?? string.Empty,
                Status = race.Status,
                ResultRecordedAt = result.ResultRecordedAt,
                CreatedAt = result.CreatedAt
            }
        };
    }

    /// <summary>
    /// After a Pre-round race result is submitted, check if ALL Pre-round races in the same
    /// tournament are now completed/finished. If so, automatically generate the Final Race
    /// with the top 12 horses. Failures are swallowed so the result submission never rolls back.
    /// </summary>
    private async Task TryAutoGenerateFinalRaceAsync(Race race)
    {
        try
        {
            // Only apply to Pre-round (RoundNumber == 1)
            if (race.Round == null || race.Round.RoundNumber != 1)
                return;

            long preRoundId = race.Round.RoundId;
            long tournamentId = race.Round.TournamentId;

            // Fetch all races in this Pre-round
            var preRaces = await _repository.GetRacesByRoundIdAsync(preRoundId);
            if (preRaces.Count == 0)
                return;

            // Check if all Pre-round races are Completed or Finished
            bool allDone = preRaces.All(r =>
                string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Status, "Finished", StringComparison.OrdinalIgnoreCase));

            if (!allDone)
                return;

            // All Pre races are done — auto-generate Final Race with top 12 finalists
            Console.WriteLine($"[AutoFinal] All Pre races in tournament {tournamentId} are completed. Auto-generating Final Race...");
            await _tournamentService.GenerateFinalRaceAsync(tournamentId);
            Console.WriteLine($"[AutoFinal] Final Race generated successfully for tournament {tournamentId}.");
        }
        catch (Exception ex)
        {
            // Non-blocking: log and continue — admin can manually trigger if needed
            Console.WriteLine($"[AutoFinal] Could not auto-generate Final Race: {ex.Message}");
        }
    }
}
