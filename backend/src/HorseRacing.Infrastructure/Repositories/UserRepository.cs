using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AppUser?> GetByEmailAsync(string email)
    {
        var normalized = email.Trim().ToLower();
        return await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
    }

    public Task<bool> UsernameExistsAsync(string username) =>
        _context.Users.AnyAsync(u => u.Username.ToLower() == username.Trim().ToLower());

    public Task<bool> RefereeLicenseExistsAsync(string licenseNumber) =>
        _context.RefereeProfiles.AnyAsync(r => r.LicenseNumber.ToLower() == licenseNumber.Trim().ToLower());

    public async Task<AppUser?> GetByIdAsync(int id)
    {
        return await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id);
    }

    public async Task AddAsync(AppUser user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<Role?> GetRoleByNameAsync(string name)
    {
        return await _context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == name.ToLower());
    }

    public async Task<IEnumerable<Role>> GetRolesAsync()
    {
        return await _context.Roles.ToListAsync();
    }

    public async Task<IEnumerable<AppUser>> GetAllUsersAsync()
    {
        return await _context.Users.Include(u => u.Role).ToListAsync();
    }

    public async Task AddJockeyProfileAsync(JockeyProfile profile)
    {
        await _context.JockeyProfiles.AddAsync(profile);
    }

    public async Task AddRefereeProfileAsync(RefereeProfile profile)
    {
        await _context.RefereeProfiles.AddAsync(profile);
    }

    public async Task AddWalletAsync(Wallet wallet)
    {
        await _context.Wallets.AddAsync(wallet);
    }

    public async Task<AppUser?> GetByVerificationTokenAsync(string token)
    {
        return await _context.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.VerificationToken == token);
    }

    public async Task<int> GetActiveAdminCountAsync()
    {
        return await _context.Users.CountAsync(u => u.Role != null && u.Role.Name == "Admin" && u.Status == "Active");
    }

    public async Task<bool> HasUpcomingJockeyAssignmentsAsync(int jockeyId)
    {
        return await _context.JockeyContracts.AnyAsync(c => 
            c.JockeyId == jockeyId && 
            c.Status == "Active" && 
            c.Tournament != null && 
            (c.Tournament.Status == "PendingRegistration" || 
             c.Tournament.Status == "PendingScheduling" ||
             c.Tournament.Status == "Pending" || 
             c.Tournament.Status == "Scheduled" || 
             c.Tournament.Status == "InProgress"));
    }

    public async Task<bool> HasUpcomingOwnerAssignmentsAsync(int ownerId)
    {
        var validStatuses = new[] { "PendingRegistration", "Registration Suspended", "PendingScheduling", "Pending", "Scheduled", "InProgress" };
        return await _context.Registrations.AnyAsync(r => 
            r.Horse != null && r.Horse.OwnerId == ownerId &&
            (r.Status == "Pending" || r.Status == "Approved") &&
            r.Tournament != null && 
            validStatuses.Contains(r.Tournament.Status));
    }

    public async Task<bool> HasUpcomingRefereeAssignmentsAsync(int refereeId)
    {
        var validStatuses = new[] { "Upcoming", "Scheduled", "Live", "InProgress", "Running" };
        return await _context.Set<HorseRacing.Domain.Entities.Tournaments.RaceRefereeAssignment>().AnyAsync(a => 
            a.RefereeProfile != null && a.RefereeProfile.UserId == refereeId &&
            a.Race != null &&
            validStatuses.Contains(a.Race.Status));
    }

    public async Task<bool> HasPendingSpectatorDependenciesAsync(int spectatorId)
    {
        var hasPendingBets = await _context.Set<Bet>().AnyAsync(b =>
            b.UserId == spectatorId && b.Status == "Pending");
            
        var hasPendingWithdrawals = await _context.Set<WalletTransaction>().AnyAsync(t =>
            t.Wallet != null && t.Wallet.UserId == spectatorId && t.Type == "Withdraw" && t.Status == "Pending");

        return hasPendingBets || hasPendingWithdrawals;
    }
}
