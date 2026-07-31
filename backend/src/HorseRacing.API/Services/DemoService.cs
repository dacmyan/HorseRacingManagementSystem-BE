using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HorseRacing.Application.Features.FinancialRewards.Interfaces;

namespace HorseRacing.API.Services;

public class DemoService : IDemoService
{
    private readonly AppDbContext _context;
    private readonly IBetPayoutService _betPayoutService;

    public DemoService(AppDbContext context, IBetPayoutService betPayoutService)
    {
        _context = context;
        _betPayoutService = betPayoutService;
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

                // Race Entry (Leave as Ready for betting)
                var raceEntry = new RaceEntry
                {
                    RaceId = race.RaceId,
                    Registration = registration,
                    JockeyId = jockeyProfile.JockeyId,
                    LaneNo = i + 1,
                    WinningProbability = 8.33m,
                    CurrentOdds = 12.0m,
                    Status = "Ready"
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

            tournament.Status = "Upcoming";

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

    public async Task<Tournament> StartDemoTournamentAsync(long tournamentId)
    {
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
            throw new InvalidOperationException($"Tournament {tournamentId} not found.");

        var race = await _context.Races.FirstOrDefaultAsync(r => r.Round != null && r.Round.TournamentId == tournamentId);
        if (race == null)
            throw new InvalidOperationException("No race found for this tournament.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            race.Status = "Active";
            race.RaceDate = DateTime.UtcNow;
            tournament.Status = "Active";
            tournament.StartDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return tournament;
    }

    public async Task<Tournament> PopulateTournamentAsync(long tournamentId, int count)
    {
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
            throw new InvalidOperationException($"Tournament {tournamentId} not found.");

        var existingRegistrations = await _context.Registrations
            .Where(r => r.TournamentId == tournamentId && r.Status == "Approved")
            .ToListAsync();

        if (count <= 0)
            throw new InvalidOperationException("Count must be greater than 0.");

        if (existingRegistrations.Count + count > 48)
            throw new InvalidOperationException($"Cannot add {count} horses. Tournament already has {existingRegistrations.Count} registrations and maximum capacity is 48.");

        int slotsNeeded = count;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var existingHorseIds = existingRegistrations.Select(r => r.HorseId).ToList();
            
            var existingContracts = await _context.JockeyContracts
                .Where(c => c.TournamentId == tournamentId)
                .ToListAsync();
            var existingJockeyIds = existingContracts.Select(c => c.JockeyId).ToList();

            // Fetch slotsNeeded random horses that are NOT already registered
            var horses = await _context.Horses
                .Where(h => !h.IsDeleted && h.HealthStatus == "Healthy" && !existingHorseIds.Contains(h.HorseId))
                .OrderBy(r => Guid.NewGuid())
                .Take(slotsNeeded)
                .ToListAsync();

            if (horses.Count < slotsNeeded)
                throw new InvalidOperationException($"Not enough healthy horses to seed demo. Found {horses.Count}, need {slotsNeeded}.");

            // Fetch slotsNeeded Jockeys that are NOT already contracted
            var jockeys = await _context.JockeyProfiles
                .Include(p => p.User)
                .Where(p => p.User != null && p.User.Status == "Active" && !existingJockeyIds.Contains(p.UserId))
                .OrderBy(r => Guid.NewGuid())
                .Take(slotsNeeded)
                .ToListAsync();

            if (jockeys.Count < slotsNeeded)
                throw new InvalidOperationException($"Not enough jockeys to seed demo. Found {jockeys.Count}, need {slotsNeeded}.");

            var vetRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Veterinarian");
            if (vetRole == null)
                throw new InvalidOperationException("Veterinarian role not found in database.");

            var vetUser = await _context.Users.FirstOrDefaultAsync(u => u.RoleId == vetRole.RoleId && u.Status == "Active");
            if (vetUser == null)
                throw new InvalidOperationException("No active Veterinarian found to perform medical checks.");

            var newRegistrations = new List<Registration>();

            // Generate Missing Data
            for (int i = 0; i < slotsNeeded; i++)
            {
                var horse = horses[i];
                var jockeyProfile = jockeys[i];

                var registration = new Registration
                {
                    TournamentId = tournament.TournamentId,
                    HorseId = horse.HorseId,
                    RegisteredAt = DateTime.UtcNow,
                    Status = "Approved"
                };
                _context.Registrations.Add(registration);
                newRegistrations.Add(registration);

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
            }

            // Save to ensure new registrations get their IDs
            await _context.SaveChangesAsync();

            // Consolidate and Create Race(s)
            var allRegistrations = existingRegistrations.Concat(newRegistrations).ToList();
            var allContracts = await _context.JockeyContracts.Where(c => c.TournamentId == tournament.TournamentId).ToListAsync();
            var allProfiles = await _context.JockeyProfiles.ToListAsync();

            if (allRegistrations.Count <= 12)
            {
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
                await _context.SaveChangesAsync(); 

                int lane = 1;
                foreach (var reg in allRegistrations)
                {
                    var contract = allContracts.FirstOrDefault(c => c.HorseId == reg.HorseId);
                    var profile = allProfiles.FirstOrDefault(p => p.UserId == contract?.JockeyId);

                    var raceEntry = new RaceEntry
                    {
                        RaceId = race.RaceId,
                        RegistrationId = reg.RegistrationId,
                        JockeyId = profile?.JockeyId, // Nullable, safe fallback if user didn't assign a contract
                        LaneNo = lane++,
                        WinningProbability = 8.33m,
                        CurrentOdds = 12.0m,
                        Status = "Ready"
                    };
                    _context.RaceEntries.Add(raceEntry);
                }
            }
            else
            {
                var round = new Round
                {
                    TournamentId = tournament.TournamentId,
                    Name = "Preliminary",
                    RoundNumber = 1,
                    StartDate = tournament.StartDate,
                    EndDate = tournament.EndDate,
                    Status = "Scheduled"
                };
                _context.Rounds.Add(round);
                await _context.SaveChangesAsync();

                int raceCount = (int)Math.Ceiling(allRegistrations.Count / 12.0);
                for (int i = 0; i < raceCount; i++)
                {
                    var raceRegistrations = allRegistrations.Skip(i * 12).Take(12).ToList();
                    
                    var race = new Race
                    {
                        RoundId = round.RoundId,
                        Name = $"Preliminary Heat {i + 1}",
                        RaceDate = (tournament.StartDate ?? DateTime.UtcNow.AddDays(1)).AddHours(i),
                        DistanceMeter = 1000,
                        MaxLanes = 12,
                        Status = "Scheduled"
                    };
                    _context.Races.Add(race);
                    await _context.SaveChangesAsync();
                    
                    int lane = 1;
                    foreach (var reg in raceRegistrations)
                    {
                        var contract = allContracts.FirstOrDefault(c => c.HorseId == reg.HorseId);
                        var profile = allProfiles.FirstOrDefault(p => p.UserId == contract?.JockeyId);

                        var raceEntry = new RaceEntry
                        {
                            RaceId = race.RaceId,
                            RegistrationId = reg.RegistrationId,
                            JockeyId = profile?.JockeyId,
                            LaneNo = lane++,
                            WinningProbability = 8.33m,
                            CurrentOdds = 12.0m,
                            Status = "Ready"
                        };
                        _context.RaceEntries.Add(raceEntry);
                    }
                }
            }

            tournament.Status = "Upcoming";

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

    public async Task<Race> StartSingleRaceAsync(long raceId)
    {
        var race = await _context.Races.FindAsync(raceId);
        if (race == null)
            throw new InvalidOperationException($"Race {raceId} not found.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            race.Status = "Active";
            race.RaceDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return race;
    }
}
