using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HorseRacing.Application.Features.UserManagement.DTOs;
using HorseRacing.Application.Features.UserManagement.Interfaces;
using HorseRacing.Infrastructure.Persistence;

namespace HorseRacing.Infrastructure.Repositories
{
    public class AdminDashboardRepository : IAdminDashboardRepository
    {
        private readonly AppDbContext _context;

        public AdminDashboardRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<AdminPayoutDto>> GetPayoutsAsync()
        {
            return await _context.Payouts
                .Include(p => p.Bet)
                    .ThenInclude(b => b.User)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new AdminPayoutDto
                {
                    PayoutId = p.Id,
                    BetId = p.BetId,
                    RaceId = p.Bet != null ? p.Bet.RaceId : 0,
                    SpectatorName = (p.Bet != null && p.Bet.User != null) ? p.Bet.User.FullName : "Unknown",
                    Amount = p.Amount,
                    Status = "Paid",
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<AdminRegistrationDto>> GetRegistrationsAsync()
        {
            return await _context.Registrations
                .Include(r => r.Tournament)
                .Include(r => r.Horse)
                    .ThenInclude(h => h.Owner)
                .Select(r => new AdminRegistrationDto
                {
                    RegistrationId = r.RegistrationId,
                    TournamentId = r.TournamentId,
                    TournamentName = r.Tournament != null ? r.Tournament.Name : "",
                    HorseId = r.HorseId,
                    HorseName = r.Horse != null ? r.Horse.Name : "",
                    OwnerName = (r.Horse != null && r.Horse.Owner != null) ? r.Horse.Owner.FullName : "",
                    Status = r.Status,
                    HealthStatus = r.Horse != null ? r.Horse.HealthStatus : "Healthy",
                    RegisteredAt = r.RegisteredAt,
                    JockeyContractStatus = _context.JockeyContracts
                        .Where(jc => jc.TournamentId == r.TournamentId && jc.HorseId == r.HorseId)
                        .OrderByDescending(jc => jc.CreatedAt)
                        .Select(jc => jc.Status)
                        .FirstOrDefault() ?? "NoContract",
                    JockeyName = _context.JockeyContracts
                        .Where(jc => jc.TournamentId == r.TournamentId && jc.HorseId == r.HorseId)
                        .OrderByDescending(jc => jc.CreatedAt)
                        .Select(jc => jc.Jockey != null ? jc.Jockey.FullName : null)
                        .FirstOrDefault()
                })
                .ToListAsync();
        }

        public async Task<List<AdminRefereeDto>> GetRefereesAsync()
        {
            return await _context.RefereeProfiles
                .Include(rp => rp.User)
                .Select(rp => new AdminRefereeDto
                {
                    UserId = rp.UserId,
                    RefereeId = rp.RefereeId,
                    FullName = rp.User != null ? rp.User.FullName : "",
                    Email = rp.User != null ? rp.User.Email : "",
                    LicenseNumber = rp.LicenseNumber,
                    ExperienceYears = rp.ExperienceYears,
                    Status = string.IsNullOrWhiteSpace(rp.Status) ? (rp.User != null ? rp.User.Status : "Active") : rp.Status
                })
                .ToListAsync();
        }

        public async Task<List<AdminViolationDto>> GetViolationsAsync()
        {
            return await _context.Violations
                .Include(v => v.Race)
                .Select(v => new AdminViolationDto
                {
                    ViolationId = v.Id,
                    RaceId = v.RaceId,
                    RaceName = v.Race != null ? v.Race.Name : "",
                    Type = v.Description.Contains(":") ? v.Description.Split(':', StringSplitOptions.None)[0] : "Violation",
                    Note = v.Description,
                    Penalty = v.Penalty,
                    Status = v.Status,
                    CreatedAt = DateTime.UtcNow // Using original logic
                })
                .ToListAsync();
        }

        public async Task<AdminPredictionStatsDto> GetPredictionStatsAsync()
        {
            var predictions = await _context.Predictions.ToListAsync();
            var total = predictions.Count;
            var correct = predictions.Count(p => p.IsCorrect == true);
            var wrong = predictions.Count(p => p.IsCorrect == false);
            var accuracyRate = total > 0 ? (double)correct * 100 / total : 0;

            return new AdminPredictionStatsDto
            {
                TotalPredictions = total,
                CorrectPredictions = correct,
                WrongPredictions = wrong,
                AccuracyRate = accuracyRate
            };
        }

        public async Task<List<AdminPredictionDto>> GetPredictionsAsync()
        {
            return await _context.Predictions
                .Include(p => p.User)
                .Include(p => p.Race)
                .Include(p => p.RaceEntry)
                    .ThenInclude(re => re.Registration)
                        .ThenInclude(reg => reg.Horse)
                .Select(p => new AdminPredictionDto
                {
                    PredictionId = p.PredictionId,
                    SpectatorName = p.User != null ? p.User.FullName : "Unknown",
                    RaceName = p.Race != null ? p.Race.Name : "Unknown Race",
                    PredictedWinner = (p.RaceEntry != null && p.RaceEntry.Registration != null && p.RaceEntry.Registration.Horse != null) ? p.RaceEntry.Registration.Horse.Name : "Unknown Horse",
                    Point = p.Point,
                    IsCorrect = p.IsCorrect,
                    Status = p.Status,
                    PredictedAt = p.PredictedAt
                })
                .ToListAsync();
        }

        public async Task<AdminBetStatsDto> GetBetStatsAsync()
        {
            var bets = await _context.Bets.ToListAsync();
            var totalBets = bets.Count;
            var totalAmount = bets.Sum(b => b.Amount);
            var wonBets = bets.Count(b => b.Status == "Won" || b.Status == "PaidOut");
            var pendingBets = bets.Count(b => b.Status == "Pending");
            var lostBets = bets.Count(b => b.Status == "Lost");

            var payouts = await _context.Payouts.ToListAsync();
            var totalPayoutsPaid = payouts.Sum(p => p.Amount);
            var houseProfit = totalAmount - totalPayoutsPaid;

            return new AdminBetStatsDto
            {
                TotalBets = totalBets,
                TotalAmount = totalAmount,
                WonBets = wonBets,
                PendingBets = pendingBets,
                LostBets = lostBets,
                TotalPayoutsPaid = totalPayoutsPaid,
                HouseProfit = houseProfit
            };
        }

        public async Task<List<AdminBetDto>> GetBetsAsync()
        {
            return await _context.Bets
                .Include(b => b.User)
                .Include(b => b.Race)
                .Include(b => b.Horse)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new AdminBetDto
                {
                    BetId = b.Id,
                    SpectatorName = b.User != null ? b.User.FullName : "Unknown",
                    RaceName = b.Race != null ? b.Race.Name : "Unknown Race",
                    HorseName = b.Horse != null ? b.Horse.Name : "Unknown Horse",
                    Amount = b.Amount,
                    Odds = (double)b.Odds,
                    PotentialPayout = (double)(b.Amount * b.Odds),
                    Status = b.Status,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<AdminActivityLogDto>> GetActivityLogAsync()
        {
            var activities = new List<AdminActivityLogDto>();

            var recentUsers = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(10)
                .Select(u => new AdminActivityLogDto { Type = "User", Title = "New user registered", Description = u.FullName + " (" + u.Email + ")", CreatedAt = u.CreatedAt })
                .ToListAsync();
            activities.AddRange(recentUsers);

            var recentRegistrations = await _context.Registrations
                .Include(r => r.Horse)
                .Include(r => r.Tournament)
                .OrderByDescending(r => r.RegisteredAt)
                .Take(10)
                .Select(r => new AdminActivityLogDto { Type = "Registration", Title = "Horse registration " + r.Status, Description = (r.Horse != null ? r.Horse.Name : "") + " registered for " + (r.Tournament != null ? r.Tournament.Name : ""), CreatedAt = r.RegisteredAt })
                .ToListAsync();
            activities.AddRange(recentRegistrations);

            var recentBets = await _context.Bets
                .Include(b => b.User)
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .Select(b => new AdminActivityLogDto { Type = "Bet", Title = "Bet placed", Description = (b.User != null ? b.User.FullName : "Unknown") + " bet " + b.Amount + " on race " + b.RaceId, CreatedAt = b.CreatedAt })
                .ToListAsync();
            activities.AddRange(recentBets);

            var recentNotifications = await _context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .Select(n => new AdminActivityLogDto { Type = "Notification", Title = "System notification", Description = n.Message, CreatedAt = n.CreatedAt })
                .ToListAsync();
            activities.AddRange(recentNotifications);

            var recentTransactions = await _context.Transactions
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .Select(t => new AdminActivityLogDto { Type = "Transaction", Title = "Wallet " + t.Type, Description = "Amount: " + t.Amount, CreatedAt = t.CreatedAt })
                .ToListAsync();
            activities.AddRange(recentTransactions);

            return activities
                .OrderByDescending(a => a.CreatedAt)
                .Take(50)
                .ToList();
        }

        public async Task<List<AdminRefereeReportDto>> GetRefereeReportsAsync()
        {
            return await _context.RefereeReports
                .AsNoTracking()
                .OrderByDescending(report => report.CreatedAt)
                .Select(report => new AdminRefereeReportDto
                {
                    reportId = report.ReportId,
                    assignmentId = report.AssignmentId,
                    raceId = report.Assignment != null ? report.Assignment.RaceId : 0,
                    raceName = report.Assignment != null && report.Assignment.Race != null
                        ? report.Assignment.Race.Name : string.Empty,
                    tournamentId = report.Assignment != null && report.Assignment.Race != null && report.Assignment.Race.Round != null
                        ? report.Assignment.Race.Round.TournamentId : 0,
                    tournamentName = report.Assignment != null && report.Assignment.Race != null && report.Assignment.Race.Round != null && report.Assignment.Race.Round.Tournament != null
                        ? report.Assignment.Race.Round.Tournament.Name : string.Empty,
                    refereeId = report.Assignment != null ? report.Assignment.RefereeId : 0,
                    refereeName = report.Assignment != null && report.Assignment.RefereeProfile != null && report.Assignment.RefereeProfile.User != null
                        ? report.Assignment.RefereeProfile.User.FullName : "Unknown Referee",
                    Content = report.Content,
                    ViolationNote = report.ViolationNote,
                    ReportedUserId = report.ReportedUserId,
                    reportedUserName = report.ReportedUser != null ? report.ReportedUser.FullName : null,
                    ReportedHorseId = report.ReportedHorseId,
                    reportedHorseName = report.ReportedHorse != null ? report.ReportedHorse.Name : null,
                    CreatedAt = report.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<AdminUserOptionDto>> GetUserOptionsAsync()
        {
            return await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Status == "Active")
                .Select(u => new AdminUserOptionDto
                {
                    Id = u.UserId,
                    Label = u.FullName,
                    Extra = u.Role != null ? u.Role.Name : "Unknown"
                })
                .ToListAsync();
        }

        public async Task<List<AdminUserOptionDto>> GetHorseOptionsAsync()
        {
            return await _context.Horses
                .Include(h => h.Owner)
                .Select(h => new AdminUserOptionDto
                {
                    Id = (int)h.HorseId,
                    Label = h.Name,
                    Extra = "Owner: " + (h.Owner != null ? h.Owner.FullName : "Unknown")
                })
                .ToListAsync();
        }

        public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalTournaments = await _context.Tournaments.CountAsync();
            var activeRaces = await _context.Races.CountAsync(r => r.Status == "Live" || r.Status == "Scheduled");
            var totalBets = await _context.Bets.CountAsync();
            
            var totalRevenue = await _context.Bets.Where(b => b.Status != "Pending").SumAsync(b => (decimal?)b.Amount) ?? 0;
            var totalPayout = await _context.Payouts.SumAsync(p => (decimal?)p.Amount) ?? 0;

            return new AdminDashboardStatsDto
            {
                TotalUsers = totalUsers,
                TotalTournaments = totalTournaments,
                ActiveRaces = activeRaces,
                TotalBets = totalBets,
                TotalRevenue = totalRevenue,
                TotalPayout = totalPayout,
                Profit = totalRevenue - totalPayout
            };
        }

        public async Task<List<AdminRaceRefereeDto>> GetRacesRefereeAssignmentsAsync()
        {
            return await _context.Races
                .Include(r => r.Round)
                    .ThenInclude(rd => rd.Tournament)
                .Include(r => r.RaceRefereeAssignments)
                    .ThenInclude(ra => ra.RefereeProfile)
                        .ThenInclude(rp => rp.User)
                .Select(r => new AdminRaceRefereeDto
                {
                    RaceId = r.RaceId,
                    RaceName = r.Name,
                    RaceDate = r.RaceDate,
                    Status = r.Status,
                    DistanceMeter = r.DistanceMeter,
                    RoundName = r.Round != null ? r.Round.Name : "",
                    TournamentName = (r.Round != null && r.Round.Tournament != null) ? r.Round.Tournament.Name : "",
                    Referees = r.RaceRefereeAssignments.Select(ra => new AdminRefereeAssignmentDto
                    {
                        RefereeId = ra.RefereeId,
                        FullName = (ra.RefereeProfile != null && ra.RefereeProfile.User != null) ? ra.RefereeProfile.User.FullName : "",
                        LicenseNumber = ra.RefereeProfile != null ? ra.RefereeProfile.LicenseNumber : "",
                        Status = ra.Status
                    }).ToList()
                })
                .ToListAsync();
        }
    }
}
