using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.API.Services;

public class DemoService : IDemoService
{
    private readonly AppDbContext _context;

    public DemoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Tournament> SetupDemoTournamentAsync()
    {
        // 1. Open transaction
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 2. Create Tournament
            var tournament = new Tournament
            {
                Name = "Auto Demo Cup " + Guid.NewGuid().ToString().Substring(0, 8),
                RegistrationStartDate = DateTime.UtcNow.AddDays(-10),
                RegistrationEndDate = DateTime.UtcNow.AddDays(-1),
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(7),
                Status = "PendingScheduling",
                Description = "Auto-generated demo tournament for presentations."
            };

            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync(); // Save to get the TournamentId

            // 3. Fetch exactly 12 random horses that are not deleted
            var horses = await _context.Horses
                .Where(h => !h.IsDeleted && h.HealthStatus == "Healthy")
                .OrderBy(r => Guid.NewGuid())
                .Take(12)
                .ToListAsync();

            if (horses.Count < 12)
            {
                throw new InvalidOperationException($"Not enough healthy horses to seed demo. Found {horses.Count}, need 12.");
            }

            // 4. Fetch exactly 12 random Jockeys
            // Assuming Jockeys are users with a specific RoleId. Let's find RoleId for Jockey.
            var jockeyRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Jockey");
            if (jockeyRole == null)
            {
                throw new InvalidOperationException("Jockey role not found in database.");
            }

            var jockeys = await _context.Users
                .Where(u => u.RoleId == jockeyRole.RoleId && u.Status == "Active")
                .OrderBy(r => Guid.NewGuid())
                .Take(12)
                .ToListAsync();

            if (jockeys.Count < 12)
            {
                throw new InvalidOperationException($"Not enough active jockeys to seed demo. Found {jockeys.Count}, need 12.");
            }

            // 4.5. Fetch one active Veterinarian
            var vetRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Veterinarian");
            if (vetRole == null)
                throw new InvalidOperationException("Veterinarian role not found in database.");

            var vetUser = await _context.Users.FirstOrDefaultAsync(u => u.RoleId == vetRole.RoleId && u.Status == "Active");
            if (vetUser == null)
                throw new InvalidOperationException("No active Veterinarian found to perform medical checks.");

            // 5. Create Registrations, Medical Checks, and Jockey Contracts
            for (int i = 0; i < 12; i++)
            {
                var horse = horses[i];
                var jockey = jockeys[i];

                // Registration
                var registration = new Registration
                {
                    TournamentId = tournament.TournamentId,
                    HorseId = horse.HorseId,
                    RegisteredAt = DateTime.UtcNow,
                    Status = "Approved"
                };
                _context.Registrations.Add(registration);


                // Medical Check
                var medicalCheck = new MedicalCheckRecord
                {
                    Registration = registration,
                    UserId = vetUser.UserId,
                    CheckType = "Initial",
                    CheckedAt = DateTime.UtcNow,
                    Temperature = 38.0m,
                    HeartRate = 35,
                    Weight = 500.0m,
                    DopingResult = "Negative",
                    MedicalResult = "Pass",
                    Notes = "Auto-passed for demo purposes."
                };
                _context.MedicalCheckRecords.Add(medicalCheck);

                // Jockey Contract
                var contract = new JockeyContract
                {
                    TournamentId = tournament.TournamentId,
                    HorseId = horse.HorseId,
                    JockeyId = jockey.UserId,
                    Status = "Active",
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(10),
                    InvitationExpiredAt = DateTime.UtcNow.AddDays(1)
                };
                _context.JockeyContracts.Add(contract);
            }

            // 6. Commit all changes
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return tournament;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
