using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.OfficiatingAndResults.DTOs;

namespace HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;

public interface IRefereeDashboardRepository
{
    Task<int?> GetRefereeIdByUserIdAsync(int userId);
    Task<List<ViolationResponse>> GetViolationsAsync(int refereeId);
    Task<List<AssignedRaceDto>> GetAssignedRacesAsync(int refereeId);
    Task<RefereeDashboardDto?> GetDashboardAsync(int refereeId);
    Task<List<HorseCheckDto>> GetHorseChecksAsync(int refereeId, long raceId);
    Task<bool> IsRefereeAssignedToRaceAsync(int refereeId, long raceId);
}
