using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;

namespace HorseRacing.Infrastructure.Persistence;

public class DataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(AppDbContext context, ILogger<DataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting mandatory data seeding...");

        try
        {
            // 1. Seed Roles
            var roles = new[]
            {
                new Role { RoleId = 1, Name = "Admin" },
                new Role { RoleId = 2, Name = "HorseOwner" },
                new Role { RoleId = 3, Name = "Jockey" },
                new Role { RoleId = 4, Name = "Referee" },
                new Role { RoleId = 5, Name = "Spectator" },
                new Role { RoleId = 6, Name = "Veterinarian" }
            };

            bool roleAdded = false;
            foreach (var role in roles)
            {
                if (!await _context.Roles.AnyAsync(r => r.RoleId == role.RoleId))
                {
                    _context.Roles.Add(role);
                    roleAdded = true;
                }
            }

            if (roleAdded)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("System roles seeded successfully.");
            }

            // 2. Seed Default Admin User
            var hasher = new PasswordHasher<AppUser>();
            var defaultAdminEmail = "admin@gmail.com";
            var defaultAdminUsername = "admin";

            if (!await _context.Users.AnyAsync(u => u.Email == defaultAdminEmail || u.Username == defaultAdminUsername))
            {
                var adminUser = new AppUser
                {
                    Username = defaultAdminUsername,
                    Email = defaultAdminEmail,
                    FullName = "System Administrator",
                    RoleId = 1, // Admin Role
                    Status = "Active",
                    IsEmailConfirmed = true, // System account — no email verification needed
                    CreatedAt = DateTime.UtcNow
                };
                adminUser.PasswordHash = hasher.HashPassword(adminUser, "123456");

                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Default Admin user ('admin@gmail.com' / '123456') seeded successfully.");
            }

            // 3. Seed Default Veterinarian User
            var defaultVetEmail = "vet@gmail.com";
            var defaultVetUsername = "vet";

            if (!await _context.Users.AnyAsync(u => u.Email == defaultVetEmail || u.Username == defaultVetUsername))
            {
                var vetUser = new AppUser
                {
                    Username = defaultVetUsername,
                    Email = defaultVetEmail,
                    FullName = "System Veterinarian",
                    RoleId = 6, // Veterinarian Role
                    Status = "Active",
                    IsEmailConfirmed = true, // System account — no email verification needed
                    CreatedAt = DateTime.UtcNow
                };
                vetUser.PasswordHash = hasher.HashPassword(vetUser, "123456");

                _context.Users.Add(vetUser);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Default Veterinarian user ('vet@gmail.com' / '123456') seeded successfully.");
            }

            // 3b. Seed Default Referee User
            var defaultRefereeEmail = "referee@gmail.com";
            var defaultRefereeUsername = "referee";

            if (!await _context.Users.AnyAsync(u => u.Email == defaultRefereeEmail || u.Username == defaultRefereeUsername))
            {
                var refereeUser = new AppUser
                {
                    Username = defaultRefereeUsername,
                    Email = defaultRefereeEmail,
                    FullName = "System Referee",
                    RoleId = 4, // Referee Role
                    Status = "Active",
                    IsEmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                refereeUser.PasswordHash = hasher.HashPassword(refereeUser, "123456");

                _context.Users.Add(refereeUser);
                await _context.SaveChangesAsync();

                _context.RefereeProfiles.Add(new RefereeProfile
                {
                    UserId = refereeUser.UserId,
                    LicenseNumber = "REF-12345",
                    Status = "Active"
                });
                await _context.SaveChangesAsync();
                _logger.LogInformation("Default Referee user ('referee@gmail.com' / '123456') seeded successfully.");
            }

            // 4. Seed 25 Test Jockey Users
            for (int i = 1; i <= 25; i++)
            {
                var jockeyUsername = $"jockeytest{i}";
                var jockeyEmail = $"jockeytest{i}@gmail.com";
                if (!await _context.Users.AnyAsync(u => u.Username == jockeyUsername || u.Email == jockeyEmail))
                {
                    var user = new AppUser
                    {
                        Username = jockeyUsername,
                        Email = jockeyEmail,
                        FullName = $"Jockey-Test{i}",
                        RoleId = 3, // Jockey
                        Status = "Active",
                        IsEmailConfirmed = true, // Seeded test account — no email verification needed
                        CreatedAt = DateTime.UtcNow
                    };
                    user.PasswordHash = hasher.HashPassword(user, "123456");
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    _context.JockeyProfiles.Add(new JockeyProfile
                    {
                        UserId = user.UserId,
                        ExperienceYears = 2 + (i % 4),
                        Status = "Active"
                    });
                    _context.Wallets.Add(new Wallet
                    {
                        UserId = user.UserId,
                        Balance = 1000m
                    });
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Test Jockey user ('{jockeyEmail}' / '123456') seeded successfully.");
                }
            }

            // 5. Seed Owner-3 and 12 Horses
            var owner3Username = "owner3";
            var owner3Email = "owner3@gmail.com";
            AppUser? owner3User = await _context.Users.FirstOrDefaultAsync(u => u.Username == owner3Username || u.Email == owner3Email);
            if (owner3User == null)
            {
                owner3User = new AppUser
                {
                    Username = owner3Username,
                    Email = owner3Email,
                    FullName = "Owner-3 (David Le)",
                    RoleId = 2, // HorseOwner Role
                    Status = "Active",
                    IsEmailConfirmed = true, // Seeded test account — no email verification needed
                    CreatedAt = DateTime.UtcNow
                };
                owner3User.PasswordHash = hasher.HashPassword(owner3User, "123456");
                _context.Users.Add(owner3User);
                await _context.SaveChangesAsync();

                _context.Wallets.Add(new Wallet
                {
                    UserId = owner3User.UserId,
                    Balance = 10000m
                });
                await _context.SaveChangesAsync();
                _logger.LogInformation("Test Owner-3 user seeded successfully.");
            }

            for (int i = 1; i <= 25; i++)
            {
                var horseName = $"Owner3-Horse{i}";
                if (!await _context.Horses.AnyAsync(h => h.Name == horseName))
                {
                    decimal avgSpeed = 14.50m;
                    decimal recentSpeed = 14.50m;
                    decimal winRate = 0.20m;

                    if (i == 1) // Very strong horse
                    {
                        avgSpeed = 17.50m;
                        recentSpeed = 18.00m;
                        winRate = 0.80m;
                    }
                    else if (i == 2) // Very slow horse
                    {
                        avgSpeed = 11.50m;
                        recentSpeed = 11.00m;
                        winRate = 0.05m;
                    }
                    else if (i == 3) // Above average
                    {
                        avgSpeed = 15.80m;
                        recentSpeed = 16.00m;
                        winRate = 0.40m;
                    }

                    var horse = new Horse
                    {
                        Name = horseName,
                        Age = DateTime.UtcNow.AddYears(-5),
                        Gender = i % 2 == 0 ? "Stallion" : "Mare",
                        Breed = "Thoroughbred",
                        HealthStatus = "Healthy",
                        OwnerId = owner3User.UserId,
                        AverageTime = avgSpeed,
                        RecentAverageTime = recentSpeed,
                        WinRate = winRate
                    };
                    _context.Horses.Add(horse);
                    await _context.SaveChangesAsync();

                    _context.HorseStatistics.Add(new HorseStatistic
                    {
                        HorseId = horse.HorseId,
                        TotalRaces = 0,
                        TotalWins = 0,
                        TotalSecondPlaces = 0,
                        TotalThirdPlaces = 0,
                        AverageSpeed = 0m,
                        UpdatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Test Horse '{horseName}' (Speed: {avgSpeed} m/s) seeded successfully.");
                }
            }

            // 6. Seed Tournament "WC 2026"
            var targetTournamentsList = new[] { "WC 2026" };
            foreach (var tName in targetTournamentsList)
            {
                var tBlock = await _context.Tournaments.FirstOrDefaultAsync(t => t.Name == tName);
                if (tBlock == null)
                {
                    tBlock = new Tournament
                    {
                        Name = tName,
                        Description = $"{tName} Tournament Description",
                        RegistrationStartDate = DateTime.UtcNow.AddDays(-5),
                        RegistrationEndDate = DateTime.UtcNow.AddDays(5),
                        StartDate = DateTime.UtcNow.AddDays(6),
                        EndDate = DateTime.UtcNow.AddDays(15),
                        Status = "Registration Open"
                    };
                    _context.Tournaments.Add(tBlock);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Tournament '{tName}' seeded successfully.");
                }

                var tNameVet = await _context.Users.FirstOrDefaultAsync(u => u.Username == "vet");
                int tNameVetId = tNameVet?.UserId ?? 1;

                int limit = tName == "WC 2026" ? 13 : 12;
                for (int i = 1; i <= limit; i++)
                {
                    var horseName = $"Owner3-Horse{i}";
                    var jockeyUsername = $"jockeytest{i}";

                    var horse = await _context.Horses.FirstOrDefaultAsync(h => h.Name == horseName);
                    var jockey = await _context.Users.FirstOrDefaultAsync(u => u.Username == jockeyUsername);

                    if (horse != null && jockey != null)
                    {
                        // Ensure horse is Healthy so that entering race results won't fail due to previous tests
                        if (horse.HealthStatus != "Healthy")
                        {
                            horse.HealthStatus = "Healthy";
                            _context.Horses.Update(horse);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Horse '{horseName}' health status set to Healthy.");
                        }

                        // 6a. Seed JockeyContract invitation
                        var hasContract = await _context.JockeyContracts.AnyAsync(jc => jc.TournamentId == tBlock.TournamentId && jc.HorseId == horse.HorseId && jc.JockeyId == jockey.UserId);
                        if (!hasContract)
                        {
                            var contract = new JockeyContract
                            {
                                TournamentId = tBlock.TournamentId,
                                HorseId = horse.HorseId,
                                JockeyId = jockey.UserId,
                                StartDate = tBlock.StartDate ?? DateTime.UtcNow.AddDays(6),
                                EndDate = tBlock.EndDate ?? DateTime.UtcNow.AddDays(15),
                                Status = "Accepted",
                                InvitationExpiredAt = DateTime.UtcNow.AddDays(2),
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.JockeyContracts.Add(contract);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"JockeyContract invitation from Owner-3 to '{jockeyUsername}' for horse '{horseName}' in '{tName}' seeded successfully.");
                        }

                        // 6b. Seed Tournament Registration
                        var registration = await _context.Registrations.FirstOrDefaultAsync(r => r.TournamentId == tBlock.TournamentId && r.HorseId == horse.HorseId);
                        if (registration == null)
                        {
                            registration = new Registration
                            {
                                TournamentId = tBlock.TournamentId,
                                HorseId = horse.HorseId,
                                Status = "Approved",
                                RegisteredAt = DateTime.UtcNow
                            };
                            _context.Registrations.Add(registration);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Registration for horse '{horseName}' in '{tName}' seeded successfully.");
                        }
                        else if (registration.Status != "Approved")
                        {
                            registration.Status = "Approved";
                            _context.Registrations.Update(registration);
                            await _context.SaveChangesAsync();
                        }

                        // 6c. Seed passing MedicalCheckRecord
                        var hasMedicalCheck = await _context.MedicalCheckRecords.AnyAsync(mc => mc.RegistrationId == registration.RegistrationId);
                        if (!hasMedicalCheck)
                        {
                            var medicalCheck = new MedicalCheckRecord
                            {
                                RegistrationId = registration.RegistrationId,
                                UserId = tNameVetId,
                                Weight = 450.0m,
                                Temperature = 38.2m,
                                HeartRate = 40,
                                DopingResult = "Negative",
                                MedicalResult = "Pass",
                                Notes = $"Auto-seeded passing check for {tName}",
                                CheckedAt = DateTime.UtcNow
                            };
                            _context.MedicalCheckRecords.Add(medicalCheck);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"MedicalCheckRecord for Registration {registration.RegistrationId} in '{tName}' seeded successfully.");
                        }
                    }
                }

                // 6d. Automatically generate Rounds, Races, and Assignments for Spectator Betting flow
                var rounds = await _context.Rounds.Where(r => r.TournamentId == tBlock.TournamentId).ToListAsync();
                if (!rounds.Any())
                {
                    if (limit == 12)
                    {
                        // Final Round
                        var finalRound = new Round
                        {
                            TournamentId = tBlock.TournamentId,
                            Name = "Final",
                            RoundNumber = 2,
                            StartDate = tBlock.StartDate,
                            EndDate = tBlock.EndDate,
                            Status = "Scheduled"
                        };
                        _context.Rounds.Add(finalRound);
                        await _context.SaveChangesAsync();

                        var finalRace = new Race
                        {
                            RoundId = finalRound.RoundId,
                            Name = "Final Race",
                            RaceDate = tBlock.EndDate ?? DateTime.UtcNow.AddDays(10),
                            DistanceMeter = 1600,
                            MaxLanes = 12,
                            Status = "Scheduled"
                        };
                        _context.Races.Add(finalRace);
                        await _context.SaveChangesAsync();

                        // Referee assignment
                        var defaultRef = await _context.Users.FirstOrDefaultAsync(u => u.Username == "referee");
                        if (defaultRef != null)
                        {
                            _context.RaceRefereeAssignments.Add(new RaceRefereeAssignment
                            {
                                RaceId = finalRace.RaceId,
                                RefereeId = defaultRef.UserId,
                                AssignedAt = DateTime.UtcNow,
                                Status = "Active"
                            });
                            await _context.SaveChangesAsync();
                        }

                        // Race Entries
                        int lane = 1;
                        for (int i = 1; i <= limit; i++)
                        {
                            var horseName = $"Owner3-Horse{i}";
                            var horse = await _context.Horses.FirstOrDefaultAsync(h => h.Name == horseName);
                            var jockeyUsername = $"jockeytest{i}";
                            var jockey = await _context.Users.FirstOrDefaultAsync(u => u.Username == jockeyUsername);
                            var registration = await _context.Registrations.FirstOrDefaultAsync(r => r.TournamentId == tBlock.TournamentId && r.HorseId == horse.HorseId);
                            if (registration != null)
                            {
                                _context.RaceEntries.Add(new RaceEntry
                                {
                                    RaceId = finalRace.RaceId,
                                    RegistrationId = registration.RegistrationId,
                                    JockeyId = jockey?.UserId,
                                    LaneNo = lane++,
                                    Status = "Confirmed",
                                    WinningProbability = 0.5m,
                                    CurrentOdds = 2.0m
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Round and Race seeded successfully for Final round tournament {tName}.");
                    }
                    else
                    {
                        // Pre Round
                        var preRound = new Round
                        {
                            TournamentId = tBlock.TournamentId,
                            Name = "Pre",
                            RoundNumber = 1,
                            StartDate = tBlock.StartDate,
                            EndDate = tBlock.EndDate,
                            Status = "Scheduled"
                        };
                        _context.Rounds.Add(preRound);
                        await _context.SaveChangesAsync();

                        // Race 1 (7 horses)
                        var preRace1 = new Race
                        {
                            RoundId = preRound.RoundId,
                            Name = "Pre Round Race 1",
                            RaceDate = tBlock.StartDate ?? DateTime.UtcNow.AddDays(7),
                            DistanceMeter = 1600,
                            MaxLanes = 12,
                            Status = "Scheduled"
                        };
                        _context.Races.Add(preRace1);
                        await _context.SaveChangesAsync();

                        // Race 2 (6 horses)
                        var preRace2 = new Race
                        {
                            RoundId = preRound.RoundId,
                            Name = "Pre Round Race 2",
                            RaceDate = tBlock.StartDate ?? DateTime.UtcNow.AddDays(7),
                            DistanceMeter = 1600,
                            MaxLanes = 12,
                            Status = "Scheduled"
                        };
                        _context.Races.Add(preRace2);
                        await _context.SaveChangesAsync();

                        // Referee assignments
                        var defaultRef = await _context.Users.FirstOrDefaultAsync(u => u.Username == "referee");
                        if (defaultRef != null)
                        {
                            _context.RaceRefereeAssignments.Add(new RaceRefereeAssignment
                            {
                                RaceId = preRace1.RaceId,
                                RefereeId = defaultRef.UserId,
                                AssignedAt = DateTime.UtcNow,
                                Status = "Active"
                            });
                            _context.RaceRefereeAssignments.Add(new RaceRefereeAssignment
                            {
                                RaceId = preRace2.RaceId,
                                RefereeId = defaultRef.UserId,
                                AssignedAt = DateTime.UtcNow,
                                Status = "Active"
                            });
                            await _context.SaveChangesAsync();
                        }

                        // Race 1 entries (1 to 7)
                        int lane = 1;
                        for (int i = 1; i <= 7; i++)
                        {
                            var horseName = $"Owner3-Horse{i}";
                            var horse = await _context.Horses.FirstOrDefaultAsync(h => h.Name == horseName);
                            var jockeyUsername = $"jockeytest{i}";
                            var jockey = await _context.Users.FirstOrDefaultAsync(u => u.Username == jockeyUsername);
                            var registration = await _context.Registrations.FirstOrDefaultAsync(r => r.TournamentId == tBlock.TournamentId && r.HorseId == horse.HorseId);
                            if (registration != null)
                            {
                                _context.RaceEntries.Add(new RaceEntry
                                {
                                    RaceId = preRace1.RaceId,
                                    RegistrationId = registration.RegistrationId,
                                    JockeyId = jockey?.UserId,
                                    LaneNo = lane++,
                                    Status = "Confirmed",
                                    WinningProbability = 0.5m,
                                    CurrentOdds = 2.0m
                                });
                            }
                        }

                        // Race 2 entries (8 to 13)
                        lane = 1;
                        for (int i = 8; i <= 13; i++)
                        {
                            var horseName = $"Owner3-Horse{i}";
                            var horse = await _context.Horses.FirstOrDefaultAsync(h => h.Name == horseName);
                            var jockeyUsername = $"jockeytest{i}";
                            var jockey = await _context.Users.FirstOrDefaultAsync(u => u.Username == jockeyUsername);
                            var registration = await _context.Registrations.FirstOrDefaultAsync(r => r.TournamentId == tBlock.TournamentId && r.HorseId == horse.HorseId);
                            if (registration != null)
                            {
                                _context.RaceEntries.Add(new RaceEntry
                                {
                                    RaceId = preRace2.RaceId,
                                    RegistrationId = registration.RegistrationId,
                                    JockeyId = jockey?.UserId,
                                    LaneNo = lane++,
                                    Status = "Confirmed",
                                    WinningProbability = 0.5m,
                                    CurrentOdds = 2.0m
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Pre round races seeded successfully for tournament {tName}.");
                    }
                }
            }

            // 7. Seed SU Test Tournaments (SU_48_HORSE, SU_14_HORSE, SU_11_HORSE)
            await SeedSUTournamentsAsync();

            _logger.LogInformation("Mandatory data seeding completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding mandatory data.");
            throw;
        }
    }

    public async Task SeedSUTournamentsAsync()
    {
        _logger.LogInformation("Seeding SU test tournaments (SU_48_HORSE, SU_14_HORSE, SU_11_HORSE)...");

        // 1. Get default Vet user
        var vetUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "vet@gmail.com") 
            ?? await _context.Users.FirstOrDefaultAsync(u => u.RoleId == 6);
        if (vetUser == null) return;

        // 2. Ensure owner user exists
        var hasher = new PasswordHasher<AppUser>();
        var suOwner = await _context.Users.FirstOrDefaultAsync(u => u.Username == "su_owner");
        if (suOwner == null)
        {
            suOwner = new AppUser
            {
                Username = "su_owner",
                Email = "su_owner@gmail.com",
                FullName = "SU Test Horse Owner",
                RoleId = 2,
                IsEmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            suOwner.PasswordHash = hasher.HashPassword(suOwner, "123456");
            _context.Users.Add(suOwner);
            await _context.SaveChangesAsync();
        }

        // 3. Ensure 55 Jockeys exist (jockeysu1 .. jockeysu55)
        var suJockeys = new List<AppUser>();
        for (int i = 1; i <= 55; i++)
        {
            var username = $"jockeysu{i}";
            var jockey = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (jockey == null)
            {
                jockey = new AppUser
                {
                    Username = username,
                    Email = $"{username}@gmail.com",
                    FullName = $"SU Jockey #{i}",
                    RoleId = 3,
                    IsEmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                jockey.PasswordHash = hasher.HashPassword(jockey, "123456");
                _context.Users.Add(jockey);
                await _context.SaveChangesAsync();
            }
            suJockeys.Add(jockey);
        }

        // 3.5. Ensure all seeded Jockeys have a JockeyProfile (fixes corrupted data)
        var jockeysWithoutProfile = await _context.Users
            .Where(u => u.RoleId == 3 && !_context.JockeyProfiles.Any(jp => jp.UserId == u.UserId))
            .ToListAsync();

        if (jockeysWithoutProfile.Any())
        {
            foreach (var j in jockeysWithoutProfile)
            {
                _context.JockeyProfiles.Add(new JockeyProfile
                {
                    UserId = j.UserId,
                    ExperienceYears = 3,
                    RankingPoint = 0,
                    Status = "Active"
                });
            }
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Created missing JockeyProfiles for {jockeysWithoutProfile.Count} jockeys.");
        }

        // 4. Helper function to seed a tournament with N entries
        async Task SeedSingleSUTournament(string tourName, int count)
        {
            var tournament = await _context.Tournaments.FirstOrDefaultAsync(t => t.Name == tourName);
            if (tournament == null)
            {
                tournament = new Tournament
                {
                    Name = tourName,
                    Description = $"Tournament '{tourName}' with {count} registered entries for validation testing",
                    RegistrationStartDate = DateTime.UtcNow.AddDays(-5),
                    RegistrationEndDate = DateTime.UtcNow.AddDays(5),
                    StartDate = DateTime.UtcNow.AddDays(6),
                    EndDate = DateTime.UtcNow.AddDays(15),
                    Status = "Registration Open"
                };
                _context.Tournaments.Add(tournament);
                await _context.SaveChangesAsync();
            }

            for (int i = 1; i <= count; i++)
            {
                var horseName = $"SU-{tourName}-Horse{i}";
                var horse = await _context.Horses.FirstOrDefaultAsync(h => h.Name == horseName && h.OwnerId == suOwner.UserId);
                if (horse == null)
                {
                    horse = new Horse
                    {
                        Name = horseName,
                        OwnerId = suOwner.UserId,
                        Age = DateTime.UtcNow.AddYears(-4),
                        Breed = "Thoroughbred",
                        Gender = i % 2 == 0 ? "Stallion" : "Mare",
                        HealthStatus = "Healthy"
                    };
                    _context.Horses.Add(horse);
                    await _context.SaveChangesAsync();
                }

                var jockey = suJockeys[(i - 1) % suJockeys.Count];

                // a. Seed JockeyContract (Accepted)
                var hasContract = await _context.JockeyContracts.AnyAsync(jc => 
                    jc.TournamentId == tournament.TournamentId && 
                    jc.HorseId == horse.HorseId);

                if (!hasContract)
                {
                    var contract = new JockeyContract
                    {
                        TournamentId = tournament.TournamentId,
                        HorseId = horse.HorseId,
                        JockeyId = jockey.UserId,
                        StartDate = tournament.StartDate ?? DateTime.UtcNow.AddDays(6),
                        EndDate = tournament.EndDate ?? DateTime.UtcNow.AddDays(15),
                        Status = "Accepted",
                        InvitationExpiredAt = DateTime.UtcNow.AddDays(2),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.JockeyContracts.Add(contract);
                    await _context.SaveChangesAsync();
                }

                // b. Seed Tournament Registration (Pending - awaiting Admin approval)
                var registration = await _context.Registrations.FirstOrDefaultAsync(r => 
                    r.TournamentId == tournament.TournamentId && 
                    r.HorseId == horse.HorseId);

                if (registration == null)
                {
                    registration = new Registration
                    {
                        TournamentId = tournament.TournamentId,
                        HorseId = horse.HorseId,
                        Status = "Pending",
                        RegisteredAt = DateTime.UtcNow.AddHours(-12)
                    };
                    _context.Registrations.Add(registration);
                    await _context.SaveChangesAsync();
                }

                // c. Seed Medical Check Record (Pass)
                var hasMedicalCheck = await _context.MedicalCheckRecords.AnyAsync(m => 
                    m.RegistrationId == registration.RegistrationId && 
                    m.CheckType == "Initial");

                if (!hasMedicalCheck)
                {
                    var medicalCheck = new MedicalCheckRecord
                    {
                        RegistrationId = registration.RegistrationId,
                        HorseId = horse.HorseId,
                        UserId = vetUser.UserId,
                        CheckType = "Initial",
                        Weight = 480 + (i % 40),
                        Temperature = 38.0m,
                        HeartRate = 40,
                        MedicalResult = "Pass",
                        DopingResult = "Negative",
                        Notes = "Medical inspection passed. Horse is fit for tournament.",
                        CheckedAt = DateTime.UtcNow.AddHours(-10)
                    };
                    _context.MedicalCheckRecords.Add(medicalCheck);
                    await _context.SaveChangesAsync();
                }
            }

            _logger.LogInformation($"Tournament '{tourName}' seeded successfully with {count} registrations.");
        }

        await SeedSingleSUTournament("SU_48_HORSE", 50);
        await SeedSingleSUTournament("SU_14_HORSE", 14);
        await SeedSingleSUTournament("SU_11_HORSE", 11);
    }

}