using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Application.Features.OfficiatingAndResults.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Repositories;

public class ViolationRepository : IViolationRepository
{
    private readonly AppDbContext _context;

    public ViolationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Race?> GetRaceByIdAsync(long raceId)
    {
        return await _context.Races
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RaceId == raceId);
    }

    public async Task<RefereeProfile?> GetRefereeProfileByIdAsync(int refereeId)
    {
        return await _context.RefereeProfiles
            .AsNoTracking()
            .Include(rp => rp.User)
                .ThenInclude(u => u!.Role)
            .FirstOrDefaultAsync(rp => rp.RefereeId == refereeId);
    }

    public async Task<RaceRefereeAssignment?> GetAssignmentAsync(long raceId, int refereeId)
    {
        return await _context.RaceRefereeAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(rra => rra.RaceId == raceId && rra.RefereeId == refereeId);
    }

    public async Task AddViolationAsync(RaceViolation violation)
    {
        await _context.Violations.AddAsync(violation);
    }

    public async Task<List<RaceViolation>> GetViolationsByRaceIdAsync(long raceId)
    {
        return await _context.Violations
            .Include(v => v.Race)
            .Where(v => v.RaceId == raceId)
            .ToListAsync();
    }

    public async Task<RaceViolation?> GetViolationByIdAsync(long violationId)
    {
        return await _context.Violations.FindAsync(violationId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
