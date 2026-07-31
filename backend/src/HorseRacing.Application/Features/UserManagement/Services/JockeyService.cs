using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.UserManagement.Interfaces;

namespace HorseRacing.Application.Features.UserManagement.Services;

public class JockeyService : IJockeyService
{
    private readonly IJockeyRepository _repository;

    public JockeyService(IJockeyRepository repository)
    {
        _repository = repository;
    }

    public async Task<JockeyStatsDto> GetJockeyStatsAsync(int userId)
    {
        var stats = await _repository.GetJockeyStatsAsync(userId);
        if (stats == null)
        {
            throw new System.Collections.Generic.KeyNotFoundException("Jockey profile not found");
        }
        return stats;
    }

    public async Task<List<JockeyViolationDto>> GetJockeyViolationsAsync(int userId)
    {
        var violations = await _repository.GetJockeyViolationsAsync(userId);
        if (violations == null)
        {
            throw new System.Collections.Generic.KeyNotFoundException("Jockey profile not found");
        }
        return violations;
    }

    public async Task<List<JockeyAssignedHorseDto>> GetAssignedHorsesAsync(int userId)
    {
        var assignments = await _repository.GetAssignedHorsesAsync(userId);
        if (assignments == null)
        {
            throw new System.Collections.Generic.KeyNotFoundException("Jockey profile not found");
        }
        return assignments;
    }
}
