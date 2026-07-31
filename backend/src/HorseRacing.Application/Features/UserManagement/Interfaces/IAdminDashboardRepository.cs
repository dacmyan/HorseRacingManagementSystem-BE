using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.UserManagement.DTOs;

namespace HorseRacing.Application.Features.UserManagement.Interfaces
{
    public interface IAdminDashboardRepository
    {
        Task<List<AdminPayoutDto>> GetPayoutsAsync();
        Task<List<AdminRegistrationDto>> GetRegistrationsAsync();
        Task<List<AdminRefereeDto>> GetRefereesAsync();
        Task<List<AdminViolationDto>> GetViolationsAsync();
        Task<AdminPredictionStatsDto> GetPredictionStatsAsync();
        Task<List<AdminPredictionDto>> GetPredictionsAsync();
        Task<AdminBetStatsDto> GetBetStatsAsync();
        Task<List<AdminBetDto>> GetBetsAsync();
        Task<List<AdminActivityLogDto>> GetActivityLogAsync();
        Task<List<AdminRefereeReportDto>> GetRefereeReportsAsync();
        Task<List<AdminUserOptionDto>> GetUserOptionsAsync();
        Task<List<AdminUserOptionDto>> GetHorseOptionsAsync();
        Task<AdminDashboardStatsDto> GetDashboardStatsAsync();
        Task<List<AdminRaceRefereeDto>> GetRacesRefereeAssignmentsAsync();
    }
}
