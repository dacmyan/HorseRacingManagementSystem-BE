using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.Public.DTOs;
using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.HorseManagement.DTOs;

namespace HorseRacing.Application.Features.Public.Interfaces;

public interface IPublicQueryRepository
{
    Task<bool> CheckDatabaseHealthAsync();
    Task<List<JockeyRankingResponse>> GetJockeyRankingsAsync();
    Task<List<HorseRankingResponse>> GetHorseRankingsAsync();
    Task<List<TournamentListResponseDto>> GetTournamentsAsync(bool isAdmin);
    Task<TournamentDetailResponseDto?> GetTournamentDetailAsync(long tournamentId, bool isAdmin);
    Task<List<LiveRaceResponseDto>> GetLiveRacesAsync();
    Task<bool> IsTournamentVisibleAsync(long tournamentId, bool isAdmin);
}
