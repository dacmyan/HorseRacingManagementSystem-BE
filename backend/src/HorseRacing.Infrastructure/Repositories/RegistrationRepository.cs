using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace HorseRacing.Infrastructure.Repositories;

public class RegistrationRepository : IRegistrationRepository
{
    private readonly AppDbContext _context;

    public RegistrationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Registration?> GetByIdAsync(long id)
    {
        return await _context.Registrations
            .Include(r => r.Horse)
                .ThenInclude(h => h!.Owner)
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r => r.RegistrationId == id);
    }

    public async Task<Registration?> GetByHorseIdAndTournamentIdAsync(long horseId, long tournamentId)
    {
        return await _context.Registrations
            .Include(r => r.MedicalCheckRecords)
            .FirstOrDefaultAsync(r => r.HorseId == horseId && r.TournamentId == tournamentId);
    }

    public async Task<IEnumerable<Registration>> GetByOwnerIdAsync(int ownerUserId)
    {
        return await _context.Registrations
            .Include(r => r.Horse)
            .Include(r => r.Tournament)
            .Include(r => r.MedicalCheckRecords)
            .Where(r => r.Horse != null && r.Horse.OwnerId == ownerUserId)
            .OrderByDescending(r => r.RegistrationId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Registration>> GetApprovedRegistrationsByHorseIdAsync(long horseId)
    {
        return await _context.Registrations
            .Include(r => r.Tournament)
            .Where(r => r.HorseId == horseId && r.Status == "Approved")
            .ToListAsync();
    }

    public async Task AddAsync(Registration registration)
    {
        await _context.Registrations.AddAsync(registration);
    }

    public Task<bool> HasAcceptedJockeyContractAsync(long tournamentId, long horseId)
    {
        return _context.JockeyContracts.AnyAsync(contract =>
            contract.TournamentId == tournamentId &&
            contract.HorseId == horseId &&
            (contract.Status == "Accepted" || contract.Status == "Active"));
    }

    public async Task<bool> ApproveWithinCapacityAsync(long registrationId, long tournamentId, int maximumApproved)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var approvedCount = await _context.Registrations.CountAsync(registration =>
            registration.TournamentId == tournamentId && registration.Status == "Approved");
        if (approvedCount >= maximumApproved)
        {
            await transaction.RollbackAsync();
            return false;
        }

        var registration = await _context.Registrations.FirstOrDefaultAsync(item =>
            item.RegistrationId == registrationId &&
            item.TournamentId == tournamentId &&
            item.Status == "Pending");
        if (registration == null)
        {
            await transaction.RollbackAsync();
            return false;
        }

        registration.Status = "Approved";
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public void Update(Registration registration)
    {
        _context.Registrations.Update(registration);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<List<int>> GetAdminUserIdsAsync()
    {
        return await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == 1)
            .Select(u => u.UserId)
            .ToListAsync();
    }
}
