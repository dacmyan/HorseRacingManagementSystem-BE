using System.Threading;
using System.Threading.Tasks;
using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Entities.Tournaments;
using HorseRacing.Domain.Entities.Financials;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser>           Users        { get; set; }
    public DbSet<Role>              Roles        { get; set; }
    public DbSet<JockeyProfile>     JockeyProfiles { get; set; }
    public DbSet<RefereeProfile>    RefereeProfiles { get; set; }
    public DbSet<Horse>             Horses       { get; set; }
    public DbSet<Tournament>        Tournaments  { get; set; }
    public DbSet<Round>             Rounds       { get; set; }
    public DbSet<Race>              Races        { get; set; }
    public DbSet<RaceEntry>         RaceEntries  { get; set; }
    public DbSet<Registration>      Registrations { get; set; }
    public DbSet<JockeyContract>    JockeyContracts { get; set; }
    public DbSet<RaceResult>        RaceResults  { get; set; }
    public DbSet<RaceRefereeAssignment> RaceRefereeAssignments { get; set; }
    public DbSet<RaceViolation>     Violations   { get; set; }
    public DbSet<Wallet>            Wallets      { get; set; }
    public DbSet<WalletTransaction> Transactions { get; set; }

    public DbSet<Bet>               Bets         { get; set; }
    public DbSet<Payout>            Payouts      { get; set; }
    public DbSet<Prize>             Prizes       { get; set; }
    public DbSet<TournamentPrizePayout> TournamentPrizePayouts { get; set; }
    public DbSet<Notification>      Notifications { get; set; }
    public DbSet<HorseDocument>     HorseDocuments { get; set; }
    public DbSet<HorseStatistic>    HorseStatistics { get; set; }
    public DbSet<RefereeReport>     RefereeReports { get; set; }
    public DbSet<Prediction>        Predictions  { get; set; }
    public DbSet<MedicalCheckRecord> MedicalCheckRecords { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("AppUser");
            entity.HasKey(u => u.UserId);
            entity.HasOne(u => u.Role)
                .WithMany()
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");
            entity.HasKey(r => r.RoleId);
        });

        modelBuilder.Entity<JockeyProfile>(entity =>
        {
            entity.ToTable("JockeyProfile");
            entity.HasKey(jp => jp.JockeyId);
            entity.HasOne(jp => jp.User)
                .WithOne()
                .HasForeignKey<JockeyProfile>(jp => jp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefereeProfile>(entity =>
        {
            entity.ToTable("RefereeProfile");
            entity.HasKey(rp => rp.RefereeId);
            entity.HasOne(rp => rp.User)
                .WithOne()
                .HasForeignKey<RefereeProfile>(rp => rp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable("Wallet");
            entity.HasKey(w => w.WalletId);
            entity.Property(w => w.Balance).HasPrecision(18, 2);
            entity.Property(w => w.RowVersion).IsRowVersion();
            entity.HasOne(w => w.User)
                .WithOne()
                .HasForeignKey<Wallet>(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RaceEntry>(entity =>
        {
            entity.ToTable("RaceEntry");
            entity.HasKey(re => re.RaceEntryId);

            entity.HasIndex(x => new { x.RaceId, x.LaneNo })
                .IsUnique();

            entity.HasIndex(x => new { x.RaceId, x.RegistrationId })
                .IsUnique();

            entity.HasOne(re => re.Race)
                .WithMany()
                .HasForeignKey(re => re.RaceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(re => re.WinningProbability)
                .HasPrecision(5, 2);

            entity.Property(re => re.CurrentOdds)
                .HasPrecision(10, 2);

            entity.Property(re => re.FinishTime)
                .HasPrecision(10, 2);

            entity.HasOne(re => re.Registration)
                .WithMany()
                .HasForeignKey(re => re.RegistrationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(re => re.JockeyProfile)
                .WithMany()
                .HasForeignKey(re => re.JockeyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JockeyContract>(entity =>
        {
            entity.ToTable("JockeyContract");
            entity.HasKey(c => c.ContractId);

            entity.HasIndex(x => new { x.TournamentId, x.HorseId, x.JockeyId })
                .IsUnique();

            entity.HasOne(c => c.Tournament)
                .WithMany()
                .HasForeignKey(c => c.TournamentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Horse)
                .WithMany()
                .HasForeignKey(c => c.HorseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Jockey)
                .WithMany()
                .HasForeignKey(c => c.JockeyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.ToTable("Registration");
            entity.HasKey(r => r.RegistrationId);
            entity.Property(r => r.RegistrationId)
                .HasColumnName("Id")
                .HasConversion<int>();

            entity.Property(r => r.HorseId)
                .HasColumnName("HorseId")
                .HasConversion<int>();

            entity.Property(r => r.RegisteredAt)
                .HasColumnName("CreatedAt");

            entity.HasIndex(x => new { x.TournamentId, x.HorseId })
                .IsUnique();

            entity.HasOne(r => r.Tournament)
                .WithMany()
                .HasForeignKey(r => r.TournamentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Horse)
                .WithMany(h => h.Registrations)
                .HasForeignKey(r => r.HorseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Bet>(entity =>
        {
            entity.ToTable("Bet");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Amount).HasPrecision(18, 2);
            entity.Property(b => b.Odds).HasPrecision(10, 2);
            entity.HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(b => b.Race)
                .WithMany()
                .HasForeignKey(b => b.RaceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(b => b.Horse)
                .WithMany()
                .HasForeignKey(b => b.HorseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(b => b.RaceEntry)
                .WithMany()
                .HasForeignKey(b => b.RaceEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payout>(entity =>
        {
            entity.ToTable("Payout");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.HasOne(p => p.Bet)
                .WithMany()
                .HasForeignKey(p => p.BetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.ToTable("Prediction");
            entity.HasKey(p => p.PredictionId);
            
            entity.HasIndex(p => new { p.UserId, p.RaceId })
                .IsUnique();

            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Race)
                .WithMany()
                .HasForeignKey(p => p.RaceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.RaceEntry)
                .WithMany()
                .HasForeignKey(p => p.RaceEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Prize>(entity =>
        {
            entity.ToTable("Prize");
            entity.HasKey(pr => pr.Id);
            entity.Property(pr => pr.Amount).HasPrecision(18, 2);
            entity.Property(pr => pr.JockeyPercentage).HasPrecision(5, 2);
            entity.Property(pr => pr.OwnerPercentage).HasPrecision(5, 2);

            entity.HasIndex(x => new { x.TournamentId, x.RankPosition })
                .IsUnique();

            entity.HasOne(pr => pr.Tournament)
                .WithMany()
                .HasForeignKey(pr => pr.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TournamentPrizePayout>(entity =>
        {
            entity.ToTable("TournamentPrizePayout");
            entity.HasKey(tpp => tpp.Id);
            entity.Property(tpp => tpp.Amount).HasPrecision(18, 2);
            entity.HasOne(tpp => tpp.Tournament)
                .WithMany()
                .HasForeignKey(tpp => tpp.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(tpp => tpp.User)
                .WithMany()
                .HasForeignKey(tpp => tpp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notification");
            entity.HasKey(n => n.Id);
            entity.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HorseDocument>(entity =>
        {
            entity.ToTable("HorseDocument");
            entity.HasKey(hd => hd.Id);
            entity.HasOne(hd => hd.Horse)
                .WithMany(h => h.Documents)
                .HasForeignKey(hd => hd.HorseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HorseStatistic>(entity =>
        {
            entity.ToTable("HorseStatistic");
            entity.HasKey(hs => hs.Id);
            entity.Property(hs => hs.AverageSpeed).HasPrecision(10, 2);
            entity.HasOne(hs => hs.Horse)
                .WithOne(h => h.Statistic)
                .HasForeignKey<HorseStatistic>(hs => hs.HorseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RaceRefereeAssignment>(entity =>
        {
            entity.ToTable("RaceRefereeAssignment");
            entity.HasKey(rra => rra.AssignmentId);

            entity.HasIndex(x => new { x.RaceId, x.RefereeId })
                .IsUnique();

            entity.HasOne(rra => rra.Race)
                .WithMany(r => r.RaceRefereeAssignments)
                .HasForeignKey(rra => rra.RaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rra => rra.RefereeProfile)
                .WithMany()
                .HasForeignKey(rra => rra.RefereeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.ToTable("WalletTransaction");
            entity.HasKey(wt => wt.TransactionId);
            entity.Property(wt => wt.Amount).HasPrecision(18, 2);

            entity.HasOne(wt => wt.Wallet)
                .WithMany()
                .HasForeignKey(wt => wt.WalletId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(wt => wt.Bet)
                .WithMany()
                .HasForeignKey(wt => wt.BetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(wt => wt.Payout)
                .WithMany()
                .HasForeignKey(wt => wt.PayoutId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(wt => wt.TournamentPrizePayout)
                .WithMany()
                .HasForeignKey(wt => wt.PrizePayoutId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefereeReport>(entity =>
        {
            entity.ToTable("RefereeReport");
            entity.HasKey(r => r.ReportId);

            entity.HasOne(r => r.Assignment)
                .WithMany()
                .HasForeignKey(r => r.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.ReportedUser)
                .WithMany()
                .HasForeignKey(r => r.ReportedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.ReportedHorse)
                .WithMany()
                .HasForeignKey(r => r.ReportedHorseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Round>(entity =>
        {
            entity.ToTable("Round");
            entity.HasIndex(x => new { x.TournamentId, x.RoundNumber })
                .IsUnique();
        });

        modelBuilder.Entity<Horse>(entity =>
        {
            entity.ToTable("Horse");
            entity.HasKey(h => h.HorseId);
            entity.Property(h => h.HorseId)
                .HasColumnName("Id")
                .HasConversion<int>();
            entity.Property(h => h.AverageTime)
                .HasPrecision(10, 2);
            entity.Property(h => h.RecentAverageTime)
                .HasPrecision(10, 2);
            entity.Property(h => h.WinRate)
                .HasPrecision(5, 2);

            entity.HasQueryFilter(h => !h.IsDeleted);
        });
        modelBuilder.Entity<Race>().ToTable("Race");
        modelBuilder.Entity<RaceResult>(entity =>
        {
            entity.ToTable("RaceResult");
            entity.HasOne(rr => rr.Race)
                .WithMany()
                .HasForeignKey(rr => rr.RaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.ToTable("Tournament");
            entity.Property(t => t.CancelCount)
                .IsRequired()
                .HasDefaultValue(0);
        });

        modelBuilder.Entity<RaceViolation>().ToTable("RaceViolation");

        modelBuilder.Entity<MedicalCheckRecord>(entity =>
        {
            entity.ToTable("MedicalCheckRecord");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Weight).HasPrecision(10, 2);
            entity.Property(m => m.Temperature).HasPrecision(10, 2);

            entity.Property(m => m.RegistrationId)
                .HasConversion<int?>();

            entity.HasOne(m => m.Registration)
                .WithMany(r => r.MedicalCheckRecords)
                .HasForeignKey(m => m.RegistrationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(m => m.HorseId)
                .HasConversion<int?>();

            entity.HasOne(m => m.Horse)
                .WithMany()
                .HasForeignKey(m => m.HorseId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Veterinarian)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        modelBuilder.SeedData();

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var modifiedRegistrations = ChangeTracker.Entries<Registration>()
            .Where(e => (e.State == EntityState.Modified || e.State == EntityState.Added) &&
                        (e.Entity.Status == "Rejected" || e.Entity.Status == "Disqualified"))
            .Select(e => e.Entity)
            .ToList();

        if (modifiedRegistrations.Any())
        {
            foreach (var reg in modifiedRegistrations)
            {
                var contracts = await JockeyContracts
                    .Where(jc => jc.TournamentId == reg.TournamentId && jc.HorseId == reg.HorseId)
                    .ToListAsync(cancellationToken);

                foreach (var contract in contracts)
                {
                    if (contract.Status == "Pending" || contract.Status == "Accepted" || contract.Status == "Active")
                    {
                        contract.Status = "Cancelled";
                    }
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

public static class ModelBuilderExtensions
{
    public static void SeedData(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { RoleId = 1, Name = "Admin" },
            new Role { RoleId = 2, Name = "HorseOwner" },
            new Role { RoleId = 3, Name = "Jockey" },
            new Role { RoleId = 4, Name = "Referee" },
            new Role { RoleId = 5, Name = "Spectator" },
            new Role { RoleId = 6, Name = "Veterinarian" }
        );
    }
}
