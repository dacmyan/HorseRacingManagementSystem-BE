using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using HorseRacing.Infrastructure.Persistence;
using HorseRacing.Application.Features.Notifications.Interfaces;
using HorseRacing.Application.Features.TournamentAndRacing.Interfaces;
using HorseRacing.Application.Features.TournamentAndRacing.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HorseRacing.API.Services
{
    public class TournamentDeadlineWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private static DateTime VietnamNow => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time");

        public TournamentDeadlineWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var now = VietnamNow;
                        var notificationService = scope.ServiceProvider.GetService<INotificationService>();

                        // 1. Quét các giải đấu cần chuyển sang trạng thái "Registration Open"
                        var pendingOpenTournaments = await context.Tournaments
                            .Where(t => t.Status == "PendingRegistration" && t.RegistrationStartDate != null && now >= t.RegistrationStartDate)
                            .ToListAsync(stoppingToken);

                        foreach (var tournament in pendingOpenTournaments)
                        {
                            tournament.Status = "Registration Open";
                            await context.SaveChangesAsync(stoppingToken);
                            Console.WriteLine($"[SYSTEM AUTOMATION]: Tournament {tournament.TournamentId} ({tournament.Name}) is now open for registration. Status updated to Registration Open.");

                            if (notificationService != null)
                            {
                                try
                                {
                                    await notificationService.BroadcastNotificationAsync(
                                         "Tournament Registration Open",
                                         $"Registration for tournament '{tournament.Name}' is now open. Register your horses now!",
                                         "Tournament",
                                         referenceId: (int)tournament.TournamentId,
                                         actionUrl: $"/spectator/tournaments/{tournament.TournamentId}"
                                     );
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[NOTIFICATION ERROR] Failed to broadcast tournament open: {ex.Message}");
                                }
                            }
                        }

                        // 2. Quét các giải đấu đã hết hạn đăng ký nhưng ở trạng thái "PendingRegistration" hoặc "Registration Open"
                        var expiredTournaments = await context.Tournaments
                            .Where(t => t.RegistrationEndDate != null && now > t.RegistrationEndDate && (t.Status == "PendingRegistration" || t.Status == "Registration Open"))
                            .ToListAsync(stoppingToken);

                        foreach (var tournament in expiredTournaments)
                        {
                            var tournamentService = scope.ServiceProvider.GetRequiredService<ITournamentService>();
                            var closeResult = await tournamentService.CloseRegistrationAsync(tournament.TournamentId);
                            int registeredCount = closeResult.QualifiedHorses;

                            if (closeResult.CanGenerateRaces)
                            {
                                // Đủ 12 ngựa -> Đóng đăng ký bình thường và chuyển sang trạng thái "PendingScheduling" (Chờ xếp lịch)
                                tournament.Status = "PendingScheduling";
                                await context.SaveChangesAsync(stoppingToken);
                                Console.WriteLine($"[SYSTEM AUTOMATION]: Tournament {tournament.TournamentId} ({tournament.Name}) has {registeredCount} registered horses. Status updated to PendingScheduling.");

                                // Notify all registered horse owners
                                if (notificationService != null)
                                {
                                    try
                                    {
                                        var registeredOwners = await context.Registrations
                                            .Where(r => r.TournamentId == tournament.TournamentId && r.Horse != null)
                                            .Select(r => r.Horse.OwnerId)
                                            .Distinct()
                                            .ToListAsync(stoppingToken);

                                        foreach (var ownerId in registeredOwners)
                                        {
                                            await notificationService.SendNotificationToUserAsync(
                                                ownerId,
                                                "Tournament Registration Closed",
                                                $"Registration for tournament '{tournament.Name}' has closed. The scheduling phase will begin shortly.",
                                                "Tournament",
                                                referenceId: (int)tournament.TournamentId,
                                                actionUrl: "/owner/registrations"
                                            );
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[NOTIFICATION ERROR] Failed to send registration close notifications: {ex.Message}");
                                    }
                                }

                                // Auto-cancel registrations without accepted jockey
                                var tournamentRepo = scope.ServiceProvider.GetRequiredService<ITournamentRepository>();
                                var cancelledRegs = await tournamentRepo.CancelRegistrationsWithoutJockeyAsync(tournament.TournamentId);

                                if (cancelledRegs.Count > 0)
                                {
                                    if (notificationService != null)
                                    {
                                        // Group by owner to send one notification per owner
                                        var ownerGroups = cancelledRegs.GroupBy(c => c.OwnerId);
                                        foreach (var group in ownerGroups)
                                        {
                                            var horseNames = string.Join(", ", group.Select(c => c.HorseName));
                                            var tournamentName = group.First().TournamentName;
                                            try
                                            {
                                                await notificationService.SendNotificationToUserAsync(
                                                    group.Key,
                                                    "Registration automatically cancelled",
                                                    $"The registration for horse [{horseNames}] in tournament '{tournamentName}' was automatically cancelled because no jockey was accepted before registration closed.",
                                                    "System",
                                                    (int)tournament.TournamentId,
                                                    actionUrl: "/owner/registrations"
                                                );
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"[NOTIFICATION ERROR] Failed to send auto-cancel notification to owner {group.Key}: {ex.Message}");
                                            }
                                        }
                                    }
                                    Console.WriteLine($"[SYSTEM AUTOMATION]: {cancelledRegs.Count} registration(s) auto-cancelled for Tournament {tournament.TournamentId} ({tournament.Name}) due to missing jockey.");
                                }
                            }
                            else
                            {
                                // Không đủ 12 ngựa
                                if (tournament.CancelCount == 0)
                                {
                                    // Lần 1: Tự động gia hạn thêm 3 ngày
                                    tournament.Status = "Registration Suspended";
                                    await context.SaveChangesAsync(stoppingToken);
                                    if (notificationService != null)
                                    {
                                        var adminIds = await notificationService.GetActiveUserIdsByRoleAsync("Admin");
                                        foreach (var adminId in adminIds)
                                        {
                                            await notificationService.SendNotificationToUserAsync(
                                                adminId,
                                                "Registration Extension Required",
                                                registeredCount > closeResult.MaximumAllowed
                                                    ? $"Tournament '{tournament.Name}' has {registeredCount}/{closeResult.MaximumAllowed} qualified horses. Reduce the field before scheduling races."
                                                    : $"Tournament '{tournament.Name}' has only {registeredCount}/{closeResult.MinimumRequired} qualified horses. Extend registration once; the new deadline will be 48 hours before the tournament starts, or cancel the tournament.",
                                                "Tournament",
                                                (int)tournament.TournamentId,
                                                actionUrl: "/admin/tournaments");
                                        }
                                    }
                                }
                                else if (tournament.CancelCount == 1)
                                {
                                    // Lần 2: Hủy giải đấu
                                    tournament.Status = "Registration Suspended";
                                    tournament.CancelCount = 2;
                                    await context.SaveChangesAsync(stoppingToken);
                                    Console.WriteLine($"[SYSTEM AUTOMATION]: Tournament {tournament.TournamentId} ({tournament.Name}) is still below 12 qualified horses after its only extension. Admin cancellation required.");

                                    // Lấy danh sách OwnerId duy nhất của các ngựa đã đăng ký
                                    var adminIds = notificationService == null
                                        ? new List<int>()
                                        : (await notificationService.GetActiveUserIdsByRoleAsync("Admin")).ToList();

                                    if (notificationService != null)
                                    {
                                        foreach (var adminId in adminIds)
                                        {
                                            try
                                            {
                                                await notificationService.SendNotificationToUserAsync(
                                                    adminId,
                                                    "Tournament Cancellation Required",
                                                    registeredCount > closeResult.MaximumAllowed
                                                        ? $"Tournament '{tournament.Name}' still has {registeredCount}/{closeResult.MaximumAllowed} qualified horses. Reduce the field or cancel the tournament."
                                                        : $"Tournament '{tournament.Name}' still has only {registeredCount}/{closeResult.MinimumRequired} qualified horses after its one allowed extension. Please cancel the tournament.",
                                                    "Tournament",
                                                    (int)tournament.TournamentId,
                                                    actionUrl: "/admin/tournaments"
                                                );
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"[NOTIFICATION ERROR] Failed to send cancellation warning to admin {adminId}: {ex.Message}");
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // 3. Warn Admins during the final 24 hours when a tournament is not ready.
                        // A notification is sent at most once every 12 hours per tournament.
                        var readinessDeadline = now.AddHours(24);
                        var readinessTournaments = await context.Tournaments
                            .Where(t => t.StartDate != null &&
                                        t.StartDate <= readinessDeadline &&
                                        t.EndDate >= now &&
                                        t.Status != "Completed" &&
                                        t.Status != "Finished" &&
                                        t.Status != "Cancelled")
                            .ToListAsync(stoppingToken);

                        foreach (var tournament in readinessTournaments)
                        {
                            var raceIds = await (
                                from round in context.Rounds.AsNoTracking()
                                join race in context.Races.AsNoTracking() on round.RoundId equals race.RoundId
                                where round.TournamentId == tournament.TournamentId
                                select race.RaceId
                            ).ToListAsync(stoppingToken);

                            var raceIdsWithLanes = await context.RaceEntries.AsNoTracking()
                                .Where(entry => raceIds.Contains(entry.RaceId))
                                .Select(entry => entry.RaceId)
                                .Distinct()
                                .ToListAsync(stoppingToken);
                            var hasCompleteLanes = raceIds.Count > 0 && raceIdsWithLanes.Count == raceIds.Count;

                            var assignedRefereeRaceIds = await context.RaceRefereeAssignments.AsNoTracking()
                                .Where(assignment => raceIds.Contains(assignment.RaceId))
                                .Select(assignment => assignment.RaceId)
                                .Distinct()
                                .ToListAsync(stoppingToken);
                            var hasMissingReferees = raceIds.Count == 0 || assignedRefereeRaceIds.Count < raceIds.Count;

                            if (hasCompleteLanes && !hasMissingReferees)
                            {
                                continue;
                            }

                            var recentlyWarned = await context.Notifications.AsNoTracking()
                                .AnyAsync(n => n.ReferenceId == (int)tournament.TournamentId &&
                                               n.Title == "Tournament Readiness Warning" &&
                                               n.CreatedAt >= DateTime.UtcNow.AddHours(-12), stoppingToken);
                            if (recentlyWarned || notificationService == null)
                            {
                                continue;
                            }

                            var missingItems = new List<string>();
                            if (!hasCompleteLanes) missingItems.Add("lane assignments");
                            if (hasMissingReferees) missingItems.Add("referee assignments");
                            var missingText = string.Join(" and ", missingItems);
                            var adminIds = await context.Users.AsNoTracking()
                                .Where(user => user.RoleId == 1)
                                .Select(user => user.UserId)
                                .ToListAsync(stoppingToken);

                            foreach (var adminId in adminIds)
                            {
                                await notificationService.SendNotificationToUserAsync(
                                    adminId,
                                    "Tournament Readiness Warning",
                                    $"Tournament '{tournament.Name}' starts within 24 hours but is missing {missingText}. It cannot become Active until these issues are resolved.",
                                    "System",
                                    referenceId: (int)tournament.TournamentId,
                                    actionUrl: "/admin/races"
                                );
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error in TournamentDeadlineWorker: {ex.Message}");
                }

                // Quét định kỳ mỗi phút
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
