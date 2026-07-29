using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.ContractAndRegistration.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Repositories;

public class JockeyContractRepository : IJockeyContractRepository
{
    private readonly AppDbContext _context;

    public JockeyContractRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<JockeyContract?> GetByIdAsync(int id)
    {
        return await _context.JockeyContracts
            .Include(jc => jc.Horse)
                .ThenInclude(h => h.Owner)
            .Include(jc => jc.Jockey)
            .Include(jc => jc.Tournament)
            .FirstOrDefaultAsync(jc => jc.ContractId == id);
    }

    public async Task<IEnumerable<JockeyContract>> GetByJockeyIdAsync(int jockeyUserId)
    {
        return await _context.JockeyContracts
            .Include(jc => jc.Horse)
                .ThenInclude(h => h.Owner)
            .Include(jc => jc.Tournament)
            .Where(jc => jc.JockeyId == jockeyUserId)
            .ToListAsync();
    }

    public async Task<IEnumerable<JockeyContract>> GetByOwnerIdAsync(int ownerUserId)
    {
        return await _context.JockeyContracts
            .Include(jc => jc.Horse)
                .ThenInclude(h => h.Owner)
            .Include(jc => jc.Jockey)
            .Include(jc => jc.Tournament)
            .Where(jc => jc.Horse != null && jc.Horse.OwnerId == ownerUserId)
            .OrderByDescending(jc => jc.ContractId)
            .ToListAsync();
    }

    public async Task<JockeyContract?> GetActiveContractForHorseAsync(int horseId, long tournamentId)
    {
        return await _context.JockeyContracts
            .Include(jc => jc.Jockey)
            .FirstOrDefaultAsync(jc => jc.HorseId == horseId && jc.TournamentId == tournamentId && (jc.Status == "Accepted" || jc.Status == "Active"));
    }

    public async Task<JockeyContract?> GetByTournamentHorseAndJockeyAsync(long tournamentId, long horseId, int jockeyUserId)
    {
        return await _context.JockeyContracts
            .Include(jc => jc.Horse)
            .Include(jc => jc.Jockey)
            .FirstOrDefaultAsync(jc =>
                jc.TournamentId == tournamentId &&
                jc.HorseId == horseId &&
                jc.JockeyId == jockeyUserId);
    }

    public async Task<bool> HasActiveContractForJockeyInTournamentAsync(int jockeyUserId, long tournamentId)
    {
        return await _context.JockeyContracts
            .AnyAsync(jc => jc.Jockey != null && jc.Jockey.UserId == jockeyUserId
                         && jc.TournamentId == tournamentId
                         && (jc.Status == "Active" || jc.Status == "Accepted"));
    }

    public async Task<bool> HasPendingOrActiveContractForHorseInTournamentAsync(long horseId, long tournamentId)
    {
        return await _context.JockeyContracts
            .AnyAsync(jc => jc.HorseId == horseId
                         && jc.TournamentId == tournamentId
                         && (jc.Status == "Pending" || jc.Status == "Active" || jc.Status == "Accepted"));
    }

    public async Task<IEnumerable<JockeyContract>> GetOtherPendingContractsForJockeyInTournamentAsync(int jockeyUserId, long tournamentId, int excludeContractId)
    {
        return await _context.JockeyContracts
            .Where(jc => jc.Jockey != null && jc.Jockey.UserId == jockeyUserId
                         && jc.TournamentId == tournamentId
                         && jc.Status == "Pending"
                         && jc.ContractId != excludeContractId)
            .ToListAsync();
    }

    public async Task<bool> HasActiveContractForJockeyAsync(int jockeyId, long tournamentId)
    {
        return await _context.JockeyContracts
            .AnyAsync(jc => jc.JockeyId == jockeyId 
                && jc.TournamentId == tournamentId 
                && (jc.Status == "Active" || jc.Status == "Accepted"));
    }

    public async Task<List<int>> GetBusyJockeysForTournamentAsync(long tournamentId)
    {
        return await _context.JockeyContracts
            .Where(jc => jc.TournamentId == tournamentId 
                && (jc.Status == "Active" || jc.Status == "Accepted" || jc.Status == "Pending"))
            .Select(jc => jc.JockeyId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<bool> HasActiveContractForHorseAsync(long horseId, long tournamentId)
    {
        return await _context.JockeyContracts
            .AnyAsync(jc => jc.HorseId == horseId 
                && jc.TournamentId == tournamentId 
                && (jc.Status == "Pending" || jc.Status == "Active" || jc.Status == "Accepted"));
    }

    public async Task AddAsync(JockeyContract contract)
    {
        await _context.JockeyContracts.AddAsync(contract);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
