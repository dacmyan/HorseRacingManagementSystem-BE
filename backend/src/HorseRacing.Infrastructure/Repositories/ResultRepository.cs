using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Repositories;

public class ResultRepository : IResultRepository
{
    private readonly AppDbContext _context;

    public ResultRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Race?> GetRaceByIdAsync(long raceId)
    {
        return await _context.Races
            .Include(r => r.Round)
                .ThenInclude(round => round!.Tournament)
            .FirstOrDefaultAsync(r => r.RaceId == raceId);
    }

    public async Task<RaceResult?> GetResultByRaceIdAsync(long raceId)
    {
        return await _context.RaceResults
            .FirstOrDefaultAsync(rr => rr.RaceId == raceId);
    }

    public async Task<int?> GetRefereeIdByUserIdAsync(int userId)
    {
        var profile = await _context.Set<RefereeProfile>()
            .FirstOrDefaultAsync(r => r.UserId == userId);
        return profile?.RefereeId;
    }

    public async Task<RaceRefereeAssignment?> GetAssignmentAsync(long raceId, int refereeId)
    {
        return await _context.RaceRefereeAssignments
            .FirstOrDefaultAsync(rra => rra.RaceId == raceId && rra.RefereeId == refereeId && rra.Status == "Active");
    }

    public async Task<Horse?> GetHorseByIdOrNameAsync(string identifier)
    {
        if (int.TryParse(identifier, out int id))
        {
            return await _context.Horses
                .Include(h => h.Owner)
                .FirstOrDefaultAsync(h => h.HorseId == id);
        }
        return await _context.Horses
            .Include(h => h.Owner)
            .FirstOrDefaultAsync(h => h.Name.ToLower() == identifier.ToLower());
    }

    public async Task<RaceEntry?> GetRaceEntryByHorseIdAsync(long raceId, long horseId)
    {
        return await _context.RaceEntries
            .Include(re => re.Registration)
                .ThenInclude(reg => reg!.Horse)
            .Include(re => re.JockeyProfile)
                .ThenInclude(jp => jp!.User)
            .FirstOrDefaultAsync(re => re.RaceId == raceId && re.Registration != null && re.Registration.HorseId == horseId);
    }

    public async Task AddResultAsync(RaceResult result)
    {
        await _context.RaceResults.AddAsync(result);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<System.Collections.Generic.IEnumerable<RaceEntry>> GetRaceEntriesAsync(long raceId)
    {
        return await _context.RaceEntries
            .Include(re => re.Registration)
                .ThenInclude(reg => reg!.Horse)
            .Include(re => re.JockeyProfile)
                .ThenInclude(jp => jp!.User)
            .Where(re => re.RaceId == raceId)
            .ToListAsync();
    }

    public async Task UpdateHorseStatsAsync(long horseId)
    {
        var horseObj = await _context.Horses.FindAsync(horseId);
        if (horseObj == null) return;

        // Fetch all finished entries for this horse in the DB
        var horseEntries = await _context.RaceEntries
            .Include(re => re.Race)
            .Where(re => re.Registration != null && re.Registration.HorseId == horseId)
            .Where(re => re.FinishTime.HasValue && re.FinishTime.Value > 0)
            .Where(re => re.Race!.Status == "Finished" || re.Race.Status == "Completed")
            .ToListAsync();

        if (horseEntries.Count > 0)
        {
            var avgSpeed = horseEntries.Average(re => (decimal)re.Race!.DistanceMeter / re.FinishTime!.Value);
            var recentAvgSpeed = horseEntries
                .OrderByDescending(re => re.Race!.RaceDate)
                .Take(3)
                .Average(re => (decimal)re.Race!.DistanceMeter / re.FinishTime!.Value);

            var totalRaces = horseEntries.Count;
            var totalWins = horseEntries.Count(re => re.FinishPosition == 1);
            // Smoothed win rate: TotalWins / (TotalRaces + 5)
            var winRate = (decimal)totalWins / (totalRaces + 5);

            horseObj.AverageTime = Math.Round(avgSpeed, 2);
            horseObj.RecentAverageTime = Math.Round(recentAvgSpeed, 2);
            horseObj.WinRate = Math.Round(winRate, 2);
        }
    }

    public async Task<List<Race>> GetRacesByRoundIdAsync(long roundId)
    {
        return await _context.Races
            .Where(r => r.RoundId == roundId)
            .ToListAsync();
    }

    public async Task<List<int>> GetAdminUserIdsAsync()
    {
        return await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == 1)
            .Select(u => u.UserId)
            .ToListAsync();
    }

    public async Task<List<RaceRefereeAssignment>> GetAssignmentsForRaceAsync(long raceId)
    {
        return await _context.RaceRefereeAssignments
            .Include(a => a.RefereeProfile)
                .ThenInclude(p => p!.User)
            .Where(a => a.RaceId == raceId)
            .ToListAsync();
    }
}
