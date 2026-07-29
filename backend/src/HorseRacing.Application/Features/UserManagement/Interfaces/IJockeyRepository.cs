using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.UserManagement.DTOs;

namespace HorseRacing.Application.Features.UserManagement.Interfaces;

public interface IJockeyRepository
{
    Task<JockeyStatsDto?> GetJockeyStatsAsync(int userId);
    Task<List<JockeyViolationDto>?> GetJockeyViolationsAsync(int userId);
    Task<List<JockeyAssignedHorseDto>?> GetAssignedHorsesAsync(int userId);
}
