using System.Threading.Tasks;

namespace HorseRacing.Application.Features.UserManagement.Interfaces
{
    public interface IAdminActionService
    {
        Task<object> UpdateViolationStatusAsync(int id, string? status);
        Task<object> WithdrawRaceEntryAsync(long raceEntryId, string? reason);
    }
}
