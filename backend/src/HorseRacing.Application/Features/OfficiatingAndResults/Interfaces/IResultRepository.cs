using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;

namespace HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;

public interface IResultRepository
{
    Task<Race?> GetRaceByIdAsync(long raceId);
    Task<RaceResult?> GetResultByRaceIdAsync(long raceId);
    Task<int?> GetRefereeIdByUserIdAsync(int userId);
    Task<RaceRefereeAssignment?> GetAssignmentAsync(long raceId, int refereeId);
    Task<Horse?> GetHorseByIdOrNameAsync(string identifier);
    Task<RaceEntry?> GetRaceEntryByHorseIdAsync(long raceId, long horseId);
    Task AddResultAsync(RaceResult result);
    Task SaveChangesAsync();
    Task<IEnumerable<RaceEntry>> GetRaceEntriesAsync(long raceId);
    Task UpdateHorseStatsAsync(long horseId);
    Task<List<Race>> GetRacesByRoundIdAsync(long roundId);
    Task<List<int>> GetAdminUserIdsAsync();
    Task<List<RaceRefereeAssignment>> GetAssignmentsForRaceAsync(long raceId);
}
