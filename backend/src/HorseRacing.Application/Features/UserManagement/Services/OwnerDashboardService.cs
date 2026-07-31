using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.UserManagement.Interfaces;

namespace HorseRacing.Application.Features.UserManagement.Services;

public class OwnerDashboardService : IOwnerDashboardService
{
    private readonly IOwnerDashboardRepository _repository;

    public OwnerDashboardService(IOwnerDashboardRepository repository)
    {
        _repository = repository;
    }

    public async Task<OwnerDashboardDto> GetOwnerDashboardAsync(int ownerId)
    {
        return await _repository.GetOwnerDashboardAsync(ownerId);
    }

    public async Task<List<OwnerResultDto>> GetOwnerResultsAsync(int ownerId)
    {
        return await _repository.GetOwnerResultsAsync(ownerId);
    }
}
