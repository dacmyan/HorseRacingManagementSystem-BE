using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Features.ContractAndRegistration.Interfaces;

public interface IRegistrationRepository
{
    Task<Registration?> GetByIdAsync(long id);
    Task<Registration?> GetByHorseIdAndTournamentIdAsync(long horseId, long tournamentId);
    Task<IEnumerable<Registration>> GetApprovedRegistrationsByHorseIdAsync(long horseId);
    Task<IEnumerable<Registration>> GetByOwnerIdAsync(int ownerUserId);
    Task AddAsync(Registration registration);
    Task<bool> HasAcceptedJockeyContractAsync(long tournamentId, long horseId);
    Task<bool> ApproveWithinCapacityAsync(long registrationId, long tournamentId, int maximumApproved);
    void Update(Registration registration);
    Task SaveChangesAsync();
    Task<List<int>> GetAdminUserIdsAsync();
}
