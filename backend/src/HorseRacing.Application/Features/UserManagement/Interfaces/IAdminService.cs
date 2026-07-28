using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Features.UserManagement.Interfaces;

public interface IAdminService
{
    Task<CreateAccountResponseDto> CreateAccountAsync(CreateAccountRequestDto request);
    Task<IEnumerable<RoleResponseDto>> GetRolesAsync();
    Task<IEnumerable<AccountResponseDto>> GetAccountsAsync();
    Task<AppUser> UpdateUserStatusAsync(int id, int currentAdminId);
}
