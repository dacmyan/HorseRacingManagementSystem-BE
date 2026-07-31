using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Application.Features.Notifications.Interfaces;

namespace HorseRacing.Application.Features.UserManagement.Services
{
    public class AdminActionService : IAdminActionService
    {
        private readonly IAdminActionRepository _repository;
        private readonly INotificationService _notificationService;

        public AdminActionService(IAdminActionRepository repository, INotificationService notificationService)
        {
            _repository = repository;
            _notificationService = notificationService;
        }

        public async Task<object> UpdateViolationStatusAsync(int id, string? status)
        {
            var violation = await _repository.GetViolationByIdAsync(id);
            if (violation == null)
            {
                throw new System.Collections.Generic.KeyNotFoundException($"Violation with ID {id} was not found.");
            }

            var requestedStatus = status?.Trim();
            var validViolationStatuses = new[] { "Pending", "Confirmed", "Rejected" };
            if (requestedStatus == null || !validViolationStatuses.Contains(requestedStatus, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid status. Must be 'Pending', 'Confirmed', or 'Rejected'.");
            }

            requestedStatus = validViolationStatuses.First(s => s.Equals(requestedStatus, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(violation.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(violation.Status, requestedStatus, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"A {violation.Status} violation cannot be changed to {requestedStatus}.");

            violation.Status = requestedStatus;
            await _repository.UpdateViolationStatusAsync(violation);

            var refereeUserIds = await _repository.GetRefereeUserIdsForRaceAsync(violation.RaceId);
            foreach (var userId in refereeUserIds)
                await _notificationService.SendNotificationToUserAsync(
                    userId,
                    "Violation report reviewed",
                    $"Violation #{violation.Id} for race #{violation.RaceId} has been {violation.Status.ToLowerInvariant()} by Admin.",
                    "Race",
                    referenceId: (int)violation.RaceId,
                    actionUrl: "/referee/violations");

            return violation;
        }

        public async Task<object> WithdrawRaceEntryAsync(long raceEntryId, string? reason)
        {
            var entry = await _repository.GetRaceEntryWithDetailsAsync(raceEntryId);

            if (entry == null)
            {
                throw new System.Collections.Generic.KeyNotFoundException($"RaceEntry with ID {raceEntryId} was not found.");
            }

            var race = entry.Race;
            if (race == null)
            {
                throw new InvalidOperationException("Race entry is not associated with a valid race.");
            }

            var alreadyFinalStatuses = new[] { "Withdrawn", "Scratch", "DNF", "Disqualified", "Finished", "Completed" };
            if (alreadyFinalStatuses.Any(s => string.Equals(entry.Status, s, StringComparison.OrdinalIgnoreCase)))
            {
                var horseName = entry.Registration?.Horse?.Name ?? "This horse";
                if (string.Equals(entry.Status, "Withdrawn", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Horse '{horseName}' has medical/health issues and has been automatically withdrawn from the race.");
                }
                throw new InvalidOperationException($"Race entry for horse '{horseName}' is already in final status '{entry.Status}'.");
            }

            if (string.Equals(race.Status, "Finished", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(race.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot withdraw an entry from a finished/completed race.");
            }

            var isSickOrInjured = entry.Registration?.Horse != null && 
                (string.Equals(entry.Registration.Horse.HealthStatus, "Sick", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(entry.Registration.Horse.HealthStatus, "Injured", StringComparison.OrdinalIgnoreCase));

            if (!isSickOrInjured)
            {
                throw new InvalidOperationException("Cannot disqualify this horse. Only horses diagnosed by the veterinarian as Sick or Injured can be disqualified.");
            }

            var withdrawReason = string.IsNullOrWhiteSpace(reason) ? "AdminDecision" : reason.Trim();
            if (withdrawReason.Length > 500)
                throw new ArgumentException("Withdrawal reason cannot exceed 500 characters.");

            // Set status
            if (string.Equals(race.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                entry.Status = "DNF";
            }
            else
            {
                entry.Status = "Withdrawn";
            }

            entry.WithdrawReason = withdrawReason;
            entry.WithdrawTime = DateTime.UtcNow;

            if (entry.Registration != null)
            {
                entry.Registration.Status = "Disqualified";
            }

            await _repository.UpdateRaceEntryAndRegistrationAsync(entry);

            if (entry.Registration?.Horse != null)
            {
                var horse = entry.Registration.Horse;
                var notice = $"Horse '{horse.Name}' has been {entry.Status.ToLowerInvariant()} from race '{race.Name}'. Reason: {withdrawReason}.";
                await _notificationService.SendNotificationToUserAsync(
                    horse.OwnerId, "Race entry withdrawn", notice, "Race", (int)race.RaceId,
                    actionUrl: "/owner/registrations");

                var jockeyUserIds = await _repository.GetJockeyUserIdsForHorseAsync(entry.Registration.TournamentId, entry.Registration.HorseId);
                foreach (var userId in jockeyUserIds)
                    await _notificationService.SendNotificationToUserAsync(
                        userId, "Race entry withdrawn", notice, "Race", (int)race.RaceId,
                        actionUrl: "/jockey/schedule");

                var refereeUserIds = await _repository.GetRefereeUserIdsForRaceAsync(race.RaceId);
                foreach (var userId in refereeUserIds)
                    await _notificationService.SendNotificationToUserAsync(
                        userId, "Race entry withdrawn", notice, "Race", (int)race.RaceId,
                        actionUrl: "/referee/schedule");
            }

            return new { 
                raceEntryId = entry.RaceEntryId, 
                status = entry.Status, 
                healthStatus = entry.Registration?.Horse?.HealthStatus 
            };
        }
    }
}
