using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.HorseManagement.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Repositories;

public class HorseRepository : IHorseRepository
{
    private readonly AppDbContext _context;

    public HorseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Horse?> GetByIdAsync(int id)
    {
        return await _context.Horses
            .Include(h => h.Owner)
            .Include(h => h.Documents)
            .Include(h => h.Statistic)
            .FirstOrDefaultAsync(h => h.HorseId == id);
    }

    public async Task<IEnumerable<Horse>> GetByOwnerIdAsync(int ownerId)
    {
        return await _context.Horses
            .Include(h => h.Documents)
            .Include(h => h.Statistic)
            .Where(h => h.OwnerId == ownerId)
            .ToListAsync();
    }

    public async Task AddAsync(Horse horse)
    {
        await _context.Horses.AddAsync(horse);
    }

    public void Update(Horse horse)
    {
        _context.Horses.Update(horse);
    }

    public void Delete(Horse horse)
    {
        _context.Horses.Remove(horse);
    }

    public async Task<bool> HasActiveDependenciesAsync(long horseId)
    {
        var now = DateTime.UtcNow;
        return await _context.Registrations.AnyAsync(r => r.HorseId == horseId && r.Tournament != null && r.Tournament.EndDate >= now) ||
               await _context.JockeyContracts.AnyAsync(c => c.HorseId == horseId && (c.Status == "Active" || c.Status == "Accepted")) ||
               await _context.Set<RaceEntry>().AnyAsync(re => re.Registration != null && re.Registration.HorseId == horseId && re.Race != null && re.Race.RaceDate >= now);
    }

    public async Task<bool> HasHistoricalDependenciesAsync(long horseId)
    {
        return await _context.Set<RaceEntry>().AnyAsync(re => re.Registration != null && re.Registration.HorseId == horseId) ||
               await _context.MedicalCheckRecords.AnyAsync(m => m.HorseId == horseId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    // Documents
    public async Task AddDocumentAsync(HorseDocument doc)
    {
        await _context.HorseDocuments.AddAsync(doc);
    }

    public async Task<HorseDocument?> GetDocumentByIdAsync(int docId)
    {
        return await _context.HorseDocuments.FirstOrDefaultAsync(d => d.Id == docId);
    }

    public void DeleteDocument(HorseDocument doc)
    {
        _context.HorseDocuments.Remove(doc);
    }

    // Statistics
    public async Task<HorseStatistic?> GetStatisticByHorseIdAsync(int horseId)
    {
        return await _context.HorseStatistics.FirstOrDefaultAsync(s => s.HorseId == horseId);
    }

    public async Task AddStatisticAsync(HorseStatistic stat)
    {
        await _context.HorseStatistics.AddAsync(stat);
    }

    public void UpdateStatistic(HorseStatistic stat)
    {
        _context.HorseStatistics.Update(stat);
    }
}
