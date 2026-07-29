using System.Threading.Tasks;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Features.UserManagement.Interfaces
{
    public interface IAdminActionRepository
    {
        Task<RaceViolation?> GetViolationByIdAsync(int id);
        Task UpdateViolationStatusAsync(RaceViolation violation);
        Task<System.Collections.Generic.List<int>> GetRefereeUserIdsForRaceAsync(long raceId);

        Task<RaceEntry?> GetRaceEntryWithDetailsAsync(long raceEntryId);
        Task UpdateRaceEntryAndRegistrationAsync(RaceEntry entry);
        Task<System.Collections.Generic.List<int>> GetJockeyUserIdsForHorseAsync(long tournamentId, long horseId);
    }
}
