using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.OfficiatingAndResults.DTOs;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Repositories;

public class RefereeDashboardRepository : IRefereeDashboardRepository
{
    private readonly AppDbContext _context;

    public RefereeDashboardRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int?> GetRefereeIdByUserIdAsync(int userId)
    {
        return await _context.RefereeProfiles
            .Where(rp => rp.UserId == userId)
            .Select(rp => (int?)rp.RefereeId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ViolationResponse>> GetViolationsAsync(int refereeId)
    {
        var assignedRaceIds = await _context.RaceRefereeAssignments
            .Where(a => a.RefereeId == refereeId && a.Status == "Active")
            .Select(a => a.RaceId)
            .ToListAsync();

        return await _context.Violations
            .Include(v => v.Race)
            .Where(v => assignedRaceIds.Contains(v.RaceId))
            .Select(v => new ViolationResponse
            {
                ViolationId = v.Id,
                RaceId = v.RaceId,
                RaceName = v.Race != null ? (v.Race.Name ?? string.Empty) : string.Empty,
                Type = v.Description.Contains(":") ? v.Description.Split(':', StringSplitOptions.None)[0] : "Violation",
                Description = v.Description,
                Penalty = v.Penalty
            })
            .ToListAsync();
    }

    public async Task<List<AssignedRaceDto>> GetAssignedRacesAsync(int refereeId)
    {
        return await _context.RaceRefereeAssignments
            .Include(rra => rra.Race)
                .ThenInclude(r => r != null ? r.Round : null)
                    .ThenInclude(round => round != null ? round.Tournament : null)
            .Where(rra => rra.RefereeId == refereeId)
            .Select(rra => new AssignedRaceDto
            {
                AssignmentId = rra.AssignmentId,
                RaceId = rra.RaceId,
                RaceName = rra.Race != null ? (rra.Race.Name ?? string.Empty) : string.Empty,
                TournamentName = (rra.Race != null && rra.Race.Round != null && rra.Race.Round.Tournament != null) ? (rra.Race.Round.Tournament.Name ?? string.Empty) : string.Empty,
                RaceDate = rra.Race != null ? rra.Race.RaceDate : null,
                Status = rra.Race != null ? (rra.Race.Status ?? string.Empty) : string.Empty
            })
            .ToListAsync();
    }

    public async Task<bool> IsRefereeAssignedToRaceAsync(int refereeId, long raceId)
    {
        return await _context.RaceRefereeAssignments
            .AnyAsync(a => a.RaceId == raceId && a.RefereeId == refereeId && a.Status == "Active");
    }

    public async Task<RefereeDashboardDto?> GetDashboardAsync(int refereeId)
    {
        var assignments = await _context.RaceRefereeAssignments
            .Include(a => a.Race)
                .ThenInclude(r => r != null ? r.Round : null)
            .Where(a => a.RefereeId == refereeId && a.Status == "Active")
            .ToListAsync();

        var assignmentIds = assignments.Select(a => a.AssignmentId).ToList();

        var reports = await _context.RefereeReports
            .Where(r => assignmentIds.Contains(r.AssignmentId))
            .ToListAsync();

        var completedAssignmentIds = reports.Select(report => report.AssignmentId).Distinct().ToHashSet();
        var completedReportCount = reports.Count;
        var pendingReportCount = assignments.Count(assignment => !completedAssignmentIds.Contains(assignment.AssignmentId));
        
        var assignedRaceIds = assignments.Select(a => a.RaceId).ToList();
        var violationsCreatedCount = await _context.Violations
            .Where(v => assignedRaceIds.Contains(v.RaceId))
            .CountAsync();

        var assignedRaces = assignments.Select(a => new DashboardAssignedRaceDto {
            RaceId = a.RaceId,
            RaceName = a.Race?.Name ?? "",
            RaceDate = a.Race?.RaceDate,
            Status = a.Race?.Status ?? "Scheduled",
            TournamentId = a.Race?.Round?.TournamentId
        }).ToList();

        return new RefereeDashboardDto {
            AssignedRaceCount = assignments.Count,
            PendingReportCount = pendingReportCount,
            CompletedReportCount = completedReportCount,
            ViolationsCreatedCount = violationsCreatedCount,
            AssignedRaces = assignedRaces
        };
    }

    public async Task<List<HorseCheckDto>> GetHorseChecksAsync(int refereeId, long raceId)
    {
        var entries = await _context.RaceEntries
            .Include(re => re.Registration)
                .ThenInclude(reg => reg != null ? reg.Horse : null)
                    .ThenInclude(h => h != null ? h.Owner : null)
            .Include(re => re.JockeyProfile)
                .ThenInclude(jp => jp != null ? jp.User : null)
            .Where(re => re.RaceId == raceId)
            .ToListAsync();

        return entries.Select(re => new HorseCheckDto {
            RaceEntryId = re.RaceEntryId,
            HorseId = re.Registration?.HorseId ?? 0,
            HorseName = re.Registration?.Horse?.Name ?? "",
            OwnerName = re.Registration?.Horse?.Owner?.FullName ?? "",
            JockeyName = re.JockeyProfile?.User?.FullName ?? "",
            LaneNo = re.LaneNo,
            MedicalStatus = re.Registration?.Horse?.HealthStatus ?? "Good",
            Status = re.Status ?? string.Empty
        }).ToList();
    }
}
