using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.OfficiatingAndResults.DTOs;

namespace HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;

public interface IRefereeService
{
    Task<ViolationResponse> LogViolationAsync(LogViolationRequest request);
    Task<List<ViolationResponse>?> GetViolationsByRaceIdAsync(long raceId);
    Task<RefereeReportResponse> SubmitReportAsync(CreateRefereeReportRequest request);
    Task<List<RefereeReportResponse>?> GetReportsByRaceIdAsync(long raceId);

    Task<long> GetRefereeIdByUserIdAsync(int userId);
    Task<List<ViolationResponse>> GetViolationsAsync(int userId);
    Task<List<AssignedRaceDto>> GetAssignedRacesAsync(int userId);
    Task<RefereeDashboardDto?> GetDashboardAsync(int userId);
    Task<List<HorseCheckDto>> GetHorseChecksAsync(int userId, long raceId);
    Task<ViolationResponse> UpdateViolationAsync(int userId, long violationId, UpdateViolationRequest request);
}
