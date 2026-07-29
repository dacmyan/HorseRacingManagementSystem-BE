using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.UserManagement.DTOs;

namespace HorseRacing.Application.Features.UserManagement.Interfaces;

public interface IOwnerDashboardRepository
{
    Task<OwnerDashboardDto> GetOwnerDashboardAsync(int ownerId);
    Task<List<OwnerResultDto>> GetOwnerResultsAsync(int ownerId);
}
