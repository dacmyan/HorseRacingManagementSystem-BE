using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Repositories;

public class JockeyRepository : IJockeyRepository
{
    private readonly AppDbContext _context;

    public JockeyRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<JockeyStatsDto?> GetJockeyStatsAsync(int userId)
    {
        var jockey = await _context.JockeyProfiles
            .FirstOrDefaultAsync(jp => jp.UserId == userId);

        if (jockey == null)
        {
            return null;
        }

        var entries = await _context.RaceEntries
            .Include(re => re.Race)
            .Include(re => re.Registration)
                .ThenInclude(reg => reg.Horse)
            .Where(re => re.JockeyId == jockey.JockeyId)
            .ToListAsync();

        var raceIds = entries.Select(re => re.RaceId).ToList();

        var results = await _context.RaceResults
            .Where(rr => raceIds.Contains(rr.RaceId))
            .ToListAsync();

        int wins = 0;
        int top3 = 0;
        foreach (var entry in entries)
        {
            bool isWin = entry.FinishPosition == 1;
            if (!isWin)
            {
                var result = results.FirstOrDefault(r => r.RaceId == entry.RaceId);
                if (result != null && entry.Registration?.Horse != null)
                {
                    if (result.Winner.Equals(entry.Registration.Horse.Name, StringComparison.OrdinalIgnoreCase) ||
                        result.Winner == entry.Registration.HorseId.ToString())
                    {
                        isWin = true;
                    }
                }
            }

            if (isWin)
            {
                wins++;
                top3++;
            }
            else if (entry.FinishPosition == 2 || entry.FinishPosition == 3)
            {
                top3++;
            }
        }

        return new JockeyStatsDto
        {
            TotalRaces = entries.Count,
            Wins = wins,
            Top3 = top3,
            TotalPoints = wins * 10,
            RankingPoint = jockey.RankingPoint
        };
    }

    public async Task<List<JockeyViolationDto>?> GetJockeyViolationsAsync(int userId)
    {
        var jockey = await _context.JockeyProfiles
            .FirstOrDefaultAsync(jp => jp.UserId == userId);

        if (jockey == null)
        {
            return null;
        }

        var raceIds = await _context.RaceEntries
            .Where(re => re.JockeyId == jockey.JockeyId)
            .Select(re => re.RaceId)
            .ToListAsync();

        return await _context.Violations
            .Include(v => v.Race)
            .Where(v => raceIds.Contains(v.RaceId))
            .Select(v => new JockeyViolationDto
            {
                ViolationId = v.Id,
                RaceName = v.Race != null ? (v.Race.Name ?? string.Empty) : string.Empty,
                Type = v.Description.Contains(":") ? v.Description.Split(':', StringSplitOptions.None)[0] : "Violation",
                Note = v.Description,
                Penalty = v.Penalty,
                CreatedAt = DateTime.UtcNow // Or use an actual CreatedAt field if available
            })
            .ToListAsync();
    }

    public async Task<List<JockeyAssignedHorseDto>?> GetAssignedHorsesAsync(int userId)
    {
        var jockey = await _context.JockeyProfiles
            .FirstOrDefaultAsync(jp => jp.UserId == userId);

        if (jockey == null)
        {
            return null;
        }

        return await _context.RaceEntries
            .Include(re => re.Race)
            .Include(re => re.Registration)
                .ThenInclude(reg => reg.Horse)
            .Include(re => re.Registration)
                .ThenInclude(reg => reg.Tournament)
            .Where(re => re.JockeyId == jockey.JockeyId)
            .Select(re => new JockeyAssignedHorseDto
            {
                RaceEntryId = re.RaceEntryId,
                RaceId = re.RaceId,
                RaceName = re.Race != null ? (re.Race.Name ?? string.Empty) : string.Empty,
                RaceDate = re.Race != null ? re.Race.RaceDate : (DateTime?)null,
                HorseId = re.Registration != null ? re.Registration.HorseId : 0,
                HorseName = (re.Registration != null && re.Registration.Horse != null) ? (re.Registration.Horse.Name ?? string.Empty) : string.Empty,
                TournamentName = (re.Registration != null && re.Registration.Tournament != null) ? (re.Registration.Tournament.Name ?? string.Empty) : string.Empty,
                LaneNo = re.LaneNo,
                Status = re.Race != null ? (re.Race.Status ?? string.Empty) : (re.Status ?? string.Empty),
                FinishPosition = re.FinishPosition,
                FinishTime = re.FinishTime
            })
            .ToListAsync();
    }
}
