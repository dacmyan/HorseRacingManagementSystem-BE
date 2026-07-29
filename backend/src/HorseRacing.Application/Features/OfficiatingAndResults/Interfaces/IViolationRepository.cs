using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;

namespace HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;

public interface IViolationRepository
{
    Task<Race?> GetRaceByIdAsync(long raceId);
    Task<RefereeProfile?> GetRefereeProfileByIdAsync(int refereeId);
    Task<RaceRefereeAssignment?> GetAssignmentAsync(long raceId, int refereeId);
    Task AddViolationAsync(RaceViolation violation);
    Task<List<RaceViolation>> GetViolationsByRaceIdAsync(long raceId);
    Task<RaceViolation?> GetViolationByIdAsync(long violationId);
    Task SaveChangesAsync();
}
