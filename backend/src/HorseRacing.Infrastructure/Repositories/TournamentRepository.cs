using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.TournamentAndRacing.DTOs;
using HorseRacing.Application.Features.TournamentAndRacing.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Repositories;

public class TournamentRepository : ITournamentRepository
{
    private readonly AppDbContext _context;

    public TournamentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tournament tournament)
    {
        await _context.Tournaments.AddAsync(tournament);
    }

    public void Update(Tournament tournament)
    {
        _context.Tournaments.Update(tournament);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(long tournamentId)
    {
        return await _context.Tournaments
            .AsNoTracking()
            .AnyAsync(t => t.TournamentId == tournamentId);
    }

    public async Task<Tournament?> GetByIdAsync(long tournamentId)
    {
        return await _context.Tournaments
            .FirstOrDefaultAsync(t => t.TournamentId == tournamentId);
    }

    public async Task<Tournament?> GetByIdWithRoundsAsync(long tournamentId)
    {
        return await _context.Tournaments
            .Include(t => t.Rounds)
            .FirstOrDefaultAsync(t => t.TournamentId == tournamentId);
    }

    public async Task<List<Tournament>> GetAllAsync()
    {
        return await _context.Tournaments
            .Include(t => t.Rounds)
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();
    }

    public async Task<List<HorseRacing.Domain.Entities.Registration>> GetApprovedRegistrationsAsync(long tournamentId)
    {
        return await _context.Set<HorseRacing.Domain.Entities.Registration>()
            .Include(r => r.Horse)
            .Include(r => r.MedicalCheckRecords)
            .Where(r => r.TournamentId == tournamentId && r.Status == "Approved")
            .OrderBy(r => r.RegistrationId)
            .ToListAsync();
    }

    public async Task AddRacesAsync(IEnumerable<HorseRacing.Domain.Entities.Tournaments.Race> races)
    {
        await _context.Races.AddRangeAsync(races);
    }

    public async Task AddRaceEntriesAsync(IEnumerable<HorseRacing.Domain.Entities.RaceEntry> entries)
    {
        await _context.RaceEntries.AddRangeAsync(entries);
    }

    public async Task<List<Race>> GetRacesByRoundIdAsync(long roundId)
    {
        return await _context.Races
            .Where(r => r.RoundId == roundId)
            .ToListAsync();
    }

    public async Task<List<HorseRacing.Domain.Entities.RaceEntry>> GetRaceEntriesByRaceIdAsync(long raceId)
    {
        return await _context.RaceEntries
            .Where(re => re.RaceId == raceId)
            .ToListAsync();
    }

    public async Task<Dictionary<long, int>> GetActiveJockeyProfileIdsByHorseAsync(long tournamentId, IEnumerable<long> horseIds)
    {
        var intHorseIds = horseIds
            .Where(id => id >= int.MinValue && id <= int.MaxValue)
            .Select(id => (int)id)
            .Distinct()
            .ToList();

        if (intHorseIds.Count == 0)
        {
            return new Dictionary<long, int>();
        }

        var assignments = await (
            from contract in _context.JockeyContracts.AsNoTracking()
            join profile in _context.JockeyProfiles.AsNoTracking()
                on contract.JockeyId equals profile.UserId
            where contract.TournamentId == tournamentId
                && intHorseIds.Contains((int)contract.HorseId)
                && (contract.Status == "Active" || contract.Status == "Accepted")
                && profile.Status == "Active"
            select new
            {
                contract.HorseId,
                profile.JockeyId
            })
            .ToListAsync();

        return assignments
            .GroupBy(x => x.HorseId)
            .ToDictionary(g => g.Key, g => g.First().JockeyId);
    }

    public async Task<List<HorseRacing.Domain.Entities.Registration>> GetTopHorsesFromPrefinalAsync(long tournamentId, long prefinalRoundId)
    {
        var races = await _context.Races
            .Where(r => r.RoundId == prefinalRoundId)
            .ToListAsync();

        if (!races.Any())
        {
            return new List<HorseRacing.Domain.Entities.Registration>();
        }

        if (races.Any(r => r.Status != "Finished"))
        {
            throw new InvalidOperationException("Not all pre-final races are finished yet.");
        }

        var raceIds = races.Select(r => r.RaceId).ToList();
        var results = await _context.RaceResults
            .Where(rr => raceIds.Contains(rr.RaceId))
            .ToListAsync();

        var winnersList = new List<long>();
        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result.Winner)) continue;
            if (int.TryParse(result.Winner, out int id))
            {
                winnersList.Add(id);
            }
            else
            {
                var horse = await _context.Horses
                    .FirstOrDefaultAsync(h => h.Name.ToLower() == result.Winner.ToLower());
                if (horse != null)
                {
                    winnersList.Add(horse.HorseId);
                }
            }
        }

        winnersList = winnersList.Distinct().ToList();

        var allPrefinalEntries = await _context.RaceEntries
            .Include(re => re.Registration)
            .Where(re => raceIds.Contains(re.RaceId))
            .ToListAsync();

        var selectedHorseIds = new List<long>();

        // Winners first (if not disqualified/scratched)
        foreach (var winHorseId in winnersList)
        {
            var entry = allPrefinalEntries.FirstOrDefault(e => e.Registration?.HorseId == winHorseId);
            if (entry != null && (entry.Status == "Disqualified" || entry.Status == "Scratched"))
            {
                continue;
            }
            selectedHorseIds.Add(winHorseId);
        }

        // Fill remaining spots up to 12
        foreach (var entry in allPrefinalEntries)
        {
            if (entry.Registration == null) continue;
            if (selectedHorseIds.Count >= 12) break;

            if (entry.Status == "Disqualified" || entry.Status == "Scratched" || entry.Status != "Confirmed")
            {
                continue;
            }

            if (!selectedHorseIds.Contains(entry.Registration.HorseId))
            {
                selectedHorseIds.Add(entry.Registration.HorseId);
            }
        }

        return await _context.Registrations
            .Include(r => r.Horse)
            .Where(r => r.TournamentId == tournamentId && selectedHorseIds.Contains(r.HorseId))
            .ToListAsync();
    }

    public async Task AddRoundAsync(Round round)
    {
        await _context.Rounds.AddAsync(round);
    }

    public async Task AddRaceAsync(Race race)
    {
        await _context.Races.AddAsync(race);
    }

    public async Task RemoveRaceEntriesAsync(IEnumerable<HorseRacing.Domain.Entities.RaceEntry> entries)
    {
        _context.RaceEntries.RemoveRange(entries);
        await Task.CompletedTask;
    }

    public async Task<List<HorseRacing.Domain.Entities.RaceEntry>> GetFinalistsFromPreRoundAsync(long tournamentId, long preRoundId)
    {
        return await _context.RaceEntries
            .Include(re => re.Registration)
                .ThenInclude(reg => reg!.Horse)
            .Include(re => re.Race)
            .Where(re => re.Race.RoundId == preRoundId)
            .Where(re => re.FinishTime.HasValue && re.FinishTime.Value > 0)
            .Where(re => re.Race.Status == "Completed" || re.Race.Status == "Finished")
            .ToListAsync();
    }

    public async Task<bool> HasRaceResultsAsync(IEnumerable<long> raceIds)
    {
        return await _context.RaceResults.AnyAsync(rr => raceIds.Contains(rr.RaceId));
    }

    public async Task AddPrizeAsync(HorseRacing.Domain.Entities.Financials.Prize prize)
    {
        await _context.Prizes.AddAsync(prize);
    }

    public async Task ClearRoundsAndRacesAsync(long tournamentId)
    {
        var rounds = await _context.Rounds
            .Where(r => r.TournamentId == tournamentId)
            .ToListAsync();

        foreach (var round in rounds)
        {
            var races = await _context.Races
                .Where(r => r.RoundId == round.RoundId)
                .ToListAsync();

            foreach (var race in races)
            {
                var entries = await _context.RaceEntries
                    .Where(re => re.RaceId == race.RaceId)
                    .ToListAsync();

                _context.RaceEntries.RemoveRange(entries);
            }

            _context.Races.RemoveRange(races);
        }

        _context.Rounds.RemoveRange(rounds);
        await _context.SaveChangesAsync();
     }

    public async Task<List<HorseRacing.Domain.Entities.MedicalCheckRecord>> GetMedicalCheckRecordsForTournamentAsync(long tournamentId)
    {
        return await _context.MedicalCheckRecords
            .Where(m => m.Registration != null && m.Registration.TournamentId == tournamentId)
            .ToListAsync();
    }

    public async Task<List<HorseRacing.Domain.Entities.Registration>> GetRegistrationsByTournamentIdAsync(long tournamentId)
    {
        return await _context.Set<HorseRacing.Domain.Entities.Registration>()
            .Include(r => r.Horse)
            .Where(r => r.TournamentId == tournamentId)
            .ToListAsync();
    }

    public async Task<bool> HasOverlappingTournamentAsync(DateTime startDate, DateTime endDate, long? excludeTournamentId = null)
    {
        return await _context.Tournaments
            .AsNoTracking()
            .Where(t => t.Status != "Completed")
            .Where(t => t.StartDate.HasValue && t.EndDate.HasValue)
            .Where(t => excludeTournamentId == null || t.TournamentId != excludeTournamentId)
            .AnyAsync(t => t.StartDate.Value <= endDate && t.EndDate.Value >= startDate);
    }

    public async Task<bool> HasRacesMissingRefereesAsync(long tournamentId)
    {
        var races = await (
            from round in _context.Rounds.AsNoTracking()
            join race in _context.Races.AsNoTracking()
                on round.RoundId equals race.RoundId
            where round.TournamentId == tournamentId
            select race
        ).ToListAsync();

        if (!races.Any())
        {
            return false;
        }

        var raceIds = races.Select(r => r.RaceId).ToList();

        var assignedRaceIds = await _context.RaceRefereeAssignments.AsNoTracking()
            .Where(a => raceIds.Contains(a.RaceId))
            .Select(a => a.RaceId)
            .Distinct()
            .ToListAsync();

        return assignedRaceIds.Count < races.Count;
    }

    public async Task<bool> NameExistsAsync(string name, long? excludeTournamentId = null)
    {
        var normalizedName = name.Trim().ToLower();
        return await _context.Tournaments.AsNoTracking()
            .AnyAsync(t => t.Name.ToLower() == normalizedName &&
                (!excludeTournamentId.HasValue || t.TournamentId != excludeTournamentId.Value));
    }

    public async Task<bool> HasCompleteLaneAssignmentsAsync(long tournamentId)
    {
        var raceIds = await (
            from round in _context.Rounds.AsNoTracking()
            join race in _context.Races.AsNoTracking()
                on round.RoundId equals race.RoundId
            where round.TournamentId == tournamentId
            select race.RaceId
        ).ToListAsync();

        if (raceIds.Count == 0)
        {
            return false;
        }

        var raceIdsWithEntries = await _context.RaceEntries.AsNoTracking()
            .Where(entry => raceIds.Contains(entry.RaceId))
            .Select(entry => entry.RaceId)
            .Distinct()
            .ToListAsync();

        return raceIdsWithEntries.Count == raceIds.Count;
    }

    public async Task<Dictionary<long, (bool HasCompleteLaneAssignments, bool HasMissingReferees)>> GetReadinessByTournamentIdsAsync(
        IEnumerable<long> tournamentIds)
    {
        var ids = tournamentIds.Distinct().ToList();
        var result = ids.ToDictionary(
            id => id,
            _ => (HasCompleteLaneAssignments: false, HasMissingReferees: false));

        if (ids.Count == 0)
        {
            return result;
        }

        var tournamentRaces = await (
            from round in _context.Rounds.AsNoTracking()
            join race in _context.Races.AsNoTracking()
                on round.RoundId equals race.RoundId
            where ids.Contains(round.TournamentId)
            select new { round.TournamentId, race.RaceId }
        ).ToListAsync();

        var raceIds = tournamentRaces.Select(x => x.RaceId).Distinct().ToList();
        if (raceIds.Count == 0)
        {
            return result;
        }

        var raceIdsWithEntries = (await _context.RaceEntries.AsNoTracking()
            .Where(entry => raceIds.Contains(entry.RaceId))
            .Select(entry => entry.RaceId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        var raceIdsWithReferees = (await _context.RaceRefereeAssignments.AsNoTracking()
            .Where(assignment => raceIds.Contains(assignment.RaceId))
            .Select(assignment => assignment.RaceId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        foreach (var group in tournamentRaces.GroupBy(x => x.TournamentId))
        {
            var tournamentRaceIds = group.Select(x => x.RaceId).Distinct().ToList();
            result[group.Key] = (
                tournamentRaceIds.All(raceIdsWithEntries.Contains),
                tournamentRaceIds.Any(raceId => !raceIdsWithReferees.Contains(raceId)));
        }

        return result;
    }

    public async Task<List<int>> GetAdminUserIdsAsync()
    {
        return await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == 1)
            .Select(u => u.UserId)
            .ToListAsync();
    }

    public async Task<List<CancelledRegistrationInfo>> CancelRegistrationsWithoutJockeyAsync(long tournamentId)
    {
        // Approved registrations must also be cancelled when their accepted jockey
        // contract is no longer present at the registration deadline.
        var registrations = await _context.Registrations
            .Include(r => r.Horse)
            .Include(r => r.Tournament)
            .Where(r => r.TournamentId == tournamentId &&
                       (r.Status == "Pending" || r.Status == "PendingVet" || r.Status == "Approved"))
            .ToListAsync();

        var cancelledList = new List<CancelledRegistrationInfo>();

        foreach (var reg in registrations)
        {
            // Check if there's an accepted/active jockey contract for this horse in this tournament
            var hasAcceptedJockey = await _context.JockeyContracts
                .AnyAsync(jc => jc.TournamentId == tournamentId &&
                               jc.HorseId == reg.HorseId &&
                               (jc.Status == "Accepted" || jc.Status == "Active"));

            if (!hasAcceptedJockey)
            {
                // Cancel the registration
                reg.Status = "Cancelled";

                // Also cancel any pending jockey contracts for this horse in this tournament
                var pendingContracts = await _context.JockeyContracts
                    .Where(jc => jc.TournamentId == tournamentId &&
                                jc.HorseId == reg.HorseId &&
                                jc.Status == "Pending")
                    .ToListAsync();

                foreach (var contract in pendingContracts)
                {
                    contract.Status = "Cancelled";
                }

                cancelledList.Add(new CancelledRegistrationInfo
                {
                    RegistrationId = reg.RegistrationId,
                    OwnerId = reg.Horse?.OwnerId ?? 0,
                    HorseName = reg.Horse?.Name ?? "Unknown Horse",
                    TournamentName = reg.Tournament?.Name ?? "Unknown Tournament",
                    TournamentId = tournamentId
                });

                Console.WriteLine($"[SYSTEM AUTOMATION]: Registration #{reg.RegistrationId} (Horse: {reg.Horse?.Name}) cancelled - no accepted jockey contract when registration closed for Tournament #{tournamentId}.");
            }
        }

        if (cancelledList.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        return cancelledList;
    }

    public async Task<List<CancelledRegistrationInfo>> CancelPendingRegistrationsAsync(long tournamentId)
    {
        var registrations = await _context.Registrations
            .Include(registration => registration.Horse)
            .Include(registration => registration.Tournament)
            .Where(registration => registration.TournamentId == tournamentId &&
                (registration.Status == "Pending" || registration.Status == "PendingVet"))
            .ToListAsync();

        var cancelled = registrations.Select(registration => new CancelledRegistrationInfo
        {
            RegistrationId = registration.RegistrationId,
            OwnerId = registration.Horse?.OwnerId ?? 0,
            HorseName = registration.Horse?.Name ?? "Unknown Horse",
            TournamentName = registration.Tournament?.Name ?? "Unknown Tournament",
            TournamentId = tournamentId
        }).ToList();

        foreach (var registration in registrations)
            registration.Status = "Cancelled";

        if (registrations.Count > 0)
            await _context.SaveChangesAsync();

        return cancelled;
    }
}
