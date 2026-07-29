using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Features.UserManagement.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email);
    Task<bool> UsernameExistsAsync(string username);
    Task<bool> RefereeLicenseExistsAsync(string licenseNumber);
    Task<AppUser?> GetByIdAsync(int id);
    Task AddAsync(AppUser user);
    Task SaveChangesAsync();

    Task<Role?> GetRoleByNameAsync(string name);
    Task<IEnumerable<Role>> GetRolesAsync();
    Task<IEnumerable<AppUser>> GetAllUsersAsync();
    Task AddJockeyProfileAsync(JockeyProfile profile);
    Task AddRefereeProfileAsync(RefereeProfile profile);
    Task AddWalletAsync(Wallet wallet);
    Task<AppUser?> GetByVerificationTokenAsync(string token);
    Task<int> GetActiveAdminCountAsync();
    Task<bool> HasUpcomingJockeyAssignmentsAsync(int jockeyId);
    Task<bool> HasUpcomingOwnerAssignmentsAsync(int ownerId);
    Task<bool> HasUpcomingRefereeAssignmentsAsync(int refereeId);
    Task<bool> HasPendingSpectatorDependenciesAsync(int spectatorId);
    Task<List<string>> GetLockingConstraintsAsync(int userId, string role);
}
