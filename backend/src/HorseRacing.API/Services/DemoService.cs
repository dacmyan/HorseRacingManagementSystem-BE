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

            var jockeys = await _context.JockeyProfiles
                .Include(p => p.User)
                .Where(p => p.User != null && p.User.Status == "Active")
                .OrderBy(r => Guid.NewGuid())
                .Take(12)
                .ToListAsync();

            if (jockeys.Count < 12)
            {
                throw new InvalidOperationException($"Chi co {jockeys.Count} nai ngua co ho so, can 12 de dung giai demo.");
            }

            // 4.5. Fetch one active Veterinarian
            var vetRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Veterinarian");
            if (vetRole == null)
                throw new InvalidOperationException("Veterinarian role not found in database.");

            var vetUser = await _context.Users.FirstOrDefaultAsync(u => u.RoleId == vetRole.RoleId && u.Status == "Active");
            if (vetUser == null)
                throw new InvalidOperationException("No active Veterinarian found to perform medical checks.");

            // 4.6 Create Round and Race first to assign Race Entries
            var round = new Round
            {
                TournamentId = tournament.TournamentId,
                Name = "Finals",
                RoundNumber = 1,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                Status = "Scheduled"
            };
            _context.Rounds.Add(round);
            await _context.SaveChangesAsync();

            var race = new Race
            {
                RoundId = round.RoundId,
                Name = "Auto Demo Race",
                RaceDate = tournament.StartDate ?? DateTime.UtcNow.AddDays(1),
                DistanceMeter = 1000,
                MaxLanes = 12,
                Status = "Scheduled"
            };
            _context.Races.Add(race);
            await _context.SaveChangesAsync(); // Save to get the RaceId

            // 5. Create Registrations, Medical Checks, Jockey Contracts, and Race Entries
            for (int i = 0; i < 12; i++)
            {
                var horse = horses[i];
                var jockeyProfile = jockeys[i];

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
                    JockeyId = jockeyProfile.UserId,
                    Status = "Active",
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(10),
                    InvitationExpiredAt = DateTime.UtcNow.AddDays(1)
                };
                _context.JockeyContracts.Add(contract);

                // Race Entry (Simulate finished race)
                var raceEntry = new RaceEntry
                {
                    RaceId = race.RaceId,
                    Registration = registration,
                    JockeyId = jockeyProfile.JockeyId,
                    LaneNo = i + 1,
                    WinningProbability = 8.33m,
                    CurrentOdds = 12.0m,
                    FinishPosition = i + 1,
                    FinishTime = 80m + (decimal)i * 0.5m, // 80.0, 80.5, 81.0, etc.
                    Status = "Finished"
                };
                _context.RaceEntries.Add(raceEntry);
            }

            // 5.5 Assign Referee
            var refereeRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Referee");
            var refereeUser = await _context.Users.FirstOrDefaultAsync(u => u.RoleId == refereeRole.RoleId && u.Status == "Active");
            if (refereeUser == null) throw new InvalidOperationException("No active Referee found.");
            
            var refereeProfile = await _context.RefereeProfiles.FirstOrDefaultAsync(rp => rp.UserId == refereeUser.UserId);
            if (refereeProfile == null) throw new InvalidOperationException("No Referee Profile found for the active Referee.");

            var assignment = new RaceRefereeAssignment
            {
                RaceId = race.RaceId,
                RefereeId = refereeProfile.RefereeId,
                AssignedAt = DateTime.UtcNow,
                Status = "Assigned"
            };
            _context.RaceRefereeAssignments.Add(assignment);

            // 5.8 Add Race Result
            var raceResult = new RaceResult
            {
                RaceId = race.RaceId,
                Winner = horses[0].Name // Lane 1 horse is the winner
            };
            _context.RaceResults.Add(raceResult);

            // Fast-forward statuses
            race.Status = "Completed";
            tournament.Status = "AwaitingResults";
            tournament.EndDate = DateTime.UtcNow.AddMinutes(-10);

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
