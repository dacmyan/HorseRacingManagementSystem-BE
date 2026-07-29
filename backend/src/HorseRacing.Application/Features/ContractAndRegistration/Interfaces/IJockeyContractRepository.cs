using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Features.ContractAndRegistration.Interfaces;

public interface IJockeyContractRepository
{
    Task<JockeyContract?> GetByIdAsync(int id);
    Task<IEnumerable<JockeyContract>> GetByJockeyIdAsync(int jockeyUserId);
    Task<IEnumerable<JockeyContract>> GetByOwnerIdAsync(int ownerUserId);
    Task<JockeyContract?> GetActiveContractForHorseAsync(int horseId, long tournamentId);
    Task<JockeyContract?> GetByTournamentHorseAndJockeyAsync(long tournamentId, long horseId, int jockeyUserId);
    Task<bool> HasActiveContractForJockeyInTournamentAsync(int jockeyUserId, long tournamentId);
    Task<bool> HasPendingOrActiveContractForHorseInTournamentAsync(long horseId, long tournamentId);
    Task<IEnumerable<JockeyContract>> GetOtherPendingContractsForJockeyInTournamentAsync(int jockeyUserId, long tournamentId, int excludeContractId);
    Task<bool> HasActiveContractForJockeyAsync(int jockeyId, long tournamentId);
    Task<List<int>> GetBusyJockeysForTournamentAsync(long tournamentId);
    Task<bool> HasActiveContractForHorseAsync(long horseId, long tournamentId);
    Task AddAsync(JockeyContract contract);
    Task SaveChangesAsync();
}
