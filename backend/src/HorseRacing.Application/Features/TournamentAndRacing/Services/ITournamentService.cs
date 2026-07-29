using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.TournamentAndRacing.DTOs;

namespace HorseRacing.Application.Features.TournamentAndRacing.Services;

public interface ITournamentService
{
    Task<TournamentResponse> CreateTournamentAsync(CreateTournamentRequest request, int adminUserId = 0);
    Task<TournamentResponse?> UpdateTournamentAsync(long id, UpdateTournamentRequest request);
    Task<List<TournamentResponse>> GetAllTournamentsAsync();
    Task<TournamentResponse?> GetTournamentByIdAsync(long id);
    Task<CloseRegistrationResponse> CloseRegistrationAsync(long id, bool manualClose = false);
    Task<List<RaceScheduleResponse>> GenerateRacesForTournamentAsync(long tournamentId);
    Task<RaceScheduleResponse> GenerateFinalRaceAsync(long tournamentId);
    Task<QualifiedHorsesResponse> GetQualifiedHorsesAsync(long id);
    Task<ExtendRegistrationResponse> ExtendRegistrationAsync(long id);
    Task CancelTournamentAsync(long id, string reason);
    Task CompleteRacingAsync(long tournamentId);
    Task CompleteTournamentAsync(long tournamentId, int adminUserId);
}
