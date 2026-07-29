using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.Public.DTOs;
using HorseRacing.Application.Features.Public.Interfaces;
using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.HorseManagement.DTOs;

namespace HorseRacing.Application.Features.Public.Services;

public class PublicQueryService : IPublicQueryService
{
    private readonly IPublicQueryRepository _repository;

    public PublicQueryService(IPublicQueryRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> CheckDatabaseHealthAsync()
    {
        return await _repository.CheckDatabaseHealthAsync();
    }

    public async Task<List<JockeyRankingResponse>> GetJockeyRankingsAsync()
    {
        return await _repository.GetJockeyRankingsAsync();
    }

    public async Task<List<HorseRankingResponse>> GetHorseRankingsAsync()
    {
        return await _repository.GetHorseRankingsAsync();
    }

    public async Task<List<TournamentListResponseDto>> GetTournamentsAsync(bool isAdmin)
    {
        return await _repository.GetTournamentsAsync(isAdmin);
    }

    public async Task<TournamentDetailResponseDto?> GetTournamentDetailAsync(long tournamentId, bool isAdmin)
    {
        return await _repository.GetTournamentDetailAsync(tournamentId, isAdmin);
    }

    public async Task<List<LiveRaceResponseDto>> GetLiveRacesAsync()
    {
        return await _repository.GetLiveRacesAsync();
    }

    public async Task<bool> IsTournamentVisibleAsync(long tournamentId, bool isAdmin)
    {
        return await _repository.IsTournamentVisibleAsync(tournamentId, isAdmin);
    }
}
