using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.UserManagement.Interfaces;

namespace HorseRacing.Application.Features.UserManagement.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly IAdminDashboardRepository _repository;

        public AdminDashboardService(IAdminDashboardRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<AdminPayoutDto>> GetPayoutsAsync() => await _repository.GetPayoutsAsync();
        
        public async Task<List<AdminRegistrationDto>> GetRegistrationsAsync() => await _repository.GetRegistrationsAsync();
        
        public async Task<List<AdminRefereeDto>> GetRefereesAsync() => await _repository.GetRefereesAsync();
        
        public async Task<List<AdminViolationDto>> GetViolationsAsync() => await _repository.GetViolationsAsync();
        
        public async Task<AdminPredictionStatsDto> GetPredictionStatsAsync() => await _repository.GetPredictionStatsAsync();
        
        public async Task<List<AdminPredictionDto>> GetPredictionsAsync() => await _repository.GetPredictionsAsync();
        
        public async Task<AdminBetStatsDto> GetBetStatsAsync() => await _repository.GetBetStatsAsync();
        
        public async Task<List<AdminBetDto>> GetBetsAsync() => await _repository.GetBetsAsync();
        
        public async Task<List<AdminActivityLogDto>> GetActivityLogAsync() => await _repository.GetActivityLogAsync();
        
        public async Task<List<AdminRefereeReportDto>> GetRefereeReportsAsync() => await _repository.GetRefereeReportsAsync();
        
        public async Task<List<AdminUserOptionDto>> GetUserOptionsAsync() => await _repository.GetUserOptionsAsync();
        
        public async Task<List<AdminUserOptionDto>> GetHorseOptionsAsync() => await _repository.GetHorseOptionsAsync();
        
        public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync() => await _repository.GetDashboardStatsAsync();
        
        public async Task<List<AdminRaceRefereeDto>> GetRacesRefereeAssignmentsAsync() => await _repository.GetRacesRefereeAssignmentsAsync();
    }
}
