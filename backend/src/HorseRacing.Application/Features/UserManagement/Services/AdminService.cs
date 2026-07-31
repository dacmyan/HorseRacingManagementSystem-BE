using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace HorseRacing.Application.Features.UserManagement.Services;

public class AdminService : IAdminService
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordHasher<AppUser> _passwordHasher;

    public AdminService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = new PasswordHasher<AppUser>();
    }

    public async Task<CreateAccountResponseDto> CreateAccountAsync(CreateAccountRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.FullName = request.FullName?.Trim() ?? string.Empty;
        request.Email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        request.Role = request.Role?.Trim() ?? string.Empty;
        request.LicenseNumber = request.LicenseNumber?.Trim();

        // 1. Validation
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("Full name is required.");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8 ||
            !request.Password.Any(char.IsUpper) || !request.Password.Any(char.IsLower) ||
            !request.Password.Any(char.IsDigit) || request.Password.All(char.IsLetterOrDigit))
            throw new ArgumentException("Password must be at least 8 characters and contain uppercase, lowercase, number, and special character.");

        if (string.IsNullOrWhiteSpace(request.Role))
            throw new ArgumentException("Role is required.");

        if (request.Role.Equals("Referee", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(request.LicenseNumber))
            throw new ArgumentException("License number is required for Referee.");

        if (request.ExperienceYears is < 0 or > 80)
            throw new ArgumentException("Experience years must be between 0 and 80.");

        // 2. Uniqueness check
        var existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser != null)
            throw new ArgumentException("Email already exists.");

        if (request.Role.Equals("Referee", StringComparison.OrdinalIgnoreCase) &&
            await _userRepository.RefereeLicenseExistsAsync(request.LicenseNumber!))
            throw new ArgumentException("License number already exists.");

        // 3. Role check
        var dbRole = await _userRepository.GetRoleByNameAsync(request.Role);
        if (dbRole == null)
            throw new ArgumentException($"Role '{request.Role}' does not exist.");

        // 4. Create AppUser
        var usernameBase = request.Email.Split('@')[0];
        var username = usernameBase;
        var suffix = 1;
        while (await _userRepository.UsernameExistsAsync(username))
            username = $"{usernameBase}{suffix++}";

        var newUser = new AppUser
        {
            FullName = request.FullName,
            Email = request.Email,
            Username = username,
            RoleId = dbRole.RoleId,
            Status = "Active",
            IsEmailConfirmed = true, // Admin-created accounts don't require email verification
            CreatedAt = DateTime.UtcNow
        };
        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, request.Password);

        await _userRepository.AddAsync(newUser);
        await _userRepository.SaveChangesAsync(); // Commit so newUser gets a UserId

        // 5. Create Profile / Wallet
        if (dbRole.Name.Equals("Jockey", StringComparison.OrdinalIgnoreCase))
        {
            var jockeyProfile = new JockeyProfile
            {
                UserId = newUser.UserId,
                ExperienceYears = request.ExperienceYears ?? 0,
                RankingPoint = 0,
                Status = "Active"
            };
            await _userRepository.AddJockeyProfileAsync(jockeyProfile);
        }
        else if (dbRole.Name.Equals("Referee", StringComparison.OrdinalIgnoreCase))
        {
            var refereeProfile = new RefereeProfile
            {
                UserId = newUser.UserId,
                LicenseNumber = request.LicenseNumber!,
                ExperienceYears = request.ExperienceYears ?? 0,
                Status = "Active"
            };
            await _userRepository.AddRefereeProfileAsync(refereeProfile);
        }
        else if (dbRole.Name.Equals("Spectator", StringComparison.OrdinalIgnoreCase))
        {
            var wallet = new Wallet
            {
                UserId = newUser.UserId,
                Balance = 0
            };
            await _userRepository.AddWalletAsync(wallet);
        }

        await _userRepository.SaveChangesAsync();

        // 6. Return response DTO
        return new CreateAccountResponseDto
        {
            UserId = newUser.UserId,
            FullName = newUser.FullName,
            Email = newUser.Email,
            Role = dbRole.Name,
            Status = newUser.Status
        };
    }

    public async Task<IEnumerable<RoleResponseDto>> GetRolesAsync()
    {
        var roles = await _userRepository.GetRolesAsync();
        return roles.Select(r => new RoleResponseDto
        {
            RoleId = r.RoleId,
            Name = r.Name
        });
    }

    public async Task<IEnumerable<AccountResponseDto>> GetAccountsAsync()
    {
        var users = await _userRepository.GetAllUsersAsync();
        return users.Select(u => new AccountResponseDto
        {
            UserId = u.UserId,
            FullName = u.FullName,
            Email = u.Email,
            RoleName = u.Role?.Name ?? "Unknown",
            Status = u.Status,
            CreatedAt = u.CreatedAt
        });
    }

    public async Task<AppUser> UpdateUserStatusAsync(int id, int currentAdminId, bool forceLock = false)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {id} was not found.");
        }

        if (id == currentAdminId && string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Administrators cannot deactivate their own account.");

        var isAdmin = user.Role?.Name == "Admin";
        if (isAdmin && string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var activeAdminCount = await _userRepository.GetActiveAdminCountAsync();
            if (activeAdminCount <= 1)
                throw new InvalidOperationException("The last active administrator cannot be deactivated.");
        }

        if (string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var roleName = user.Role?.Name ?? "";
            var blockers = await _userRepository.GetLockingConstraintsAsync(id, roleName);

            if (blockers.Any() && !forceLock)
            {
                throw new HorseRacing.Domain.Exceptions.LockConstraintException(blockers);
            }
        }

        user.Status = string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";
        await _userRepository.SaveChangesAsync();

        return user;
    }
}
