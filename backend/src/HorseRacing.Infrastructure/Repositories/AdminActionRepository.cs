using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Domain.Entities;
using HorseRacing.Infrastructure.Persistence;

namespace HorseRacing.Infrastructure.Repositories
{
    public class AdminActionRepository : IAdminActionRepository
    {
        private readonly AppDbContext _context;

        public AdminActionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RaceViolation?> GetViolationByIdAsync(int id)
        {
            return await _context.Violations.FindAsync(id);
        }

        public async Task UpdateViolationStatusAsync(RaceViolation violation)
        {
            _context.Violations.Update(violation);
            await _context.SaveChangesAsync();
        }

        public async Task<List<int>> GetRefereeUserIdsForRaceAsync(long raceId)
        {
            return await _context.RaceRefereeAssignments
                .Where(a => a.RaceId == raceId && a.RefereeProfile != null)
                .Select(a => a.RefereeProfile!.UserId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<RaceEntry?> GetRaceEntryWithDetailsAsync(long raceEntryId)
        {
            return await _context.RaceEntries
                .Include(re => re.Registration)
                    .ThenInclude(reg => reg.Horse)
                .Include(re => re.Race)
                .FirstOrDefaultAsync(re => re.RaceEntryId == raceEntryId);
        }

        public async Task UpdateRaceEntryAndRegistrationAsync(RaceEntry entry)
        {
            _context.RaceEntries.Update(entry);
            if (entry.Registration != null)
            {
                _context.Registrations.Update(entry.Registration);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<List<int>> GetJockeyUserIdsForHorseAsync(long tournamentId, long horseId)
        {
            return await _context.JockeyContracts
                .Where(c => c.TournamentId == tournamentId && c.HorseId == horseId &&
                            (c.Status == "Accepted" || c.Status == "Active"))
                .Select(c => c.JockeyId)
                .Distinct()
                .ToListAsync();
        }
    }
}
