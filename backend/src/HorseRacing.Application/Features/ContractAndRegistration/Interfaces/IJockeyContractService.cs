using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Application.Features.ContractAndRegistration.DTOs;

namespace HorseRacing.Application.Features.ContractAndRegistration.Interfaces;

public interface IJockeyContractService
{
    Task<JockeyContractResponse> SendContractAsync(int ownerUserId, CreateJockeyContract request);
    Task<IEnumerable<JockeyContractResponse>> GetContractsForJockeyAsync(int jockeyUserId);
    Task<IEnumerable<JockeyContractResponse>> GetContractsForOwnerAsync(int ownerUserId);
    Task<JockeyContractResponse> RespondToContractAsync(int jockeyUserId, int contractId, RespondToContractRequest request);
    Task<JockeyContractResponse> CancelContractAsync(int ownerUserId, int contractId);
    Task<bool> CheckJockeyBusyAsync(int jockeyId, long tournamentId);
    Task<List<int>> GetBusyJockeysForTournamentAsync(long tournamentId);
    Task<bool> CheckHorseBusyAsync(int horseId, long tournamentId);
}
