using System;
using System.Collections.Generic;

namespace HorseRacing.Application.Features.UserManagement.DTOs
{
    public class AdminPayoutDto
    {
        public long PayoutId { get; set; }
        public long BetId { get; set; }
        public long RaceId { get; set; }
        public string SpectatorName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AdminRegistrationDto
    {
        public long RegistrationId { get; set; }
        public long TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public long HorseId { get; set; }
        public string HorseName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string HealthStatus { get; set; } = string.Empty;
        public DateTime RegisteredAt { get; set; }
        public string JockeyContractStatus { get; set; } = string.Empty;
        public string? JockeyName { get; set; }
    }

    public class AdminRefereeDto
    {
        public int UserId { get; set; }
        public int RefereeId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string LicenseNumber { get; set; } = string.Empty;
        public int ExperienceYears { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class AdminViolationDto
    {
        public int ViolationId { get; set; }
        public long RaceId { get; set; }
        public string RaceName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string Penalty { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AdminPredictionStatsDto
    {
        public int TotalPredictions { get; set; }
        public int CorrectPredictions { get; set; }
        public int WrongPredictions { get; set; }
        public double AccuracyRate { get; set; }
    }

    public class AdminPredictionDto
    {
        public long PredictionId { get; set; }
        public string SpectatorName { get; set; } = string.Empty;
        public string RaceName { get; set; } = string.Empty;
        public string PredictedWinner { get; set; } = string.Empty;
        public int Point { get; set; }
        public bool? IsCorrect { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime PredictedAt { get; set; }
    }

    public class AdminBetStatsDto
    {
        public int TotalBets { get; set; }
        public decimal TotalAmount { get; set; }
        public int WonBets { get; set; }
        public int PendingBets { get; set; }
        public int LostBets { get; set; }
        public decimal TotalPayoutsPaid { get; set; }
        public decimal HouseProfit { get; set; }
    }

    public class AdminBetDto
    {
        public long BetId { get; set; }
        public string SpectatorName { get; set; } = string.Empty;
        public string RaceName { get; set; } = string.Empty;
        public string HorseName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public double Odds { get; set; }
        public double PotentialPayout { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AdminActivityLogDto
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AdminRefereeReportDto
    {
        public long reportId { get; set; }
        public long assignmentId { get; set; }
        public long raceId { get; set; }
        public string raceName { get; set; } = string.Empty;
        public long tournamentId { get; set; }
        public string tournamentName { get; set; } = string.Empty;
        public int refereeId { get; set; }
        public string refereeName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string ViolationNote { get; set; } = string.Empty;
        public int? ReportedUserId { get; set; }
        public string? reportedUserName { get; set; }
        public long? ReportedHorseId { get; set; }
        public string? reportedHorseName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminUserOptionDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Extra { get; set; } = string.Empty;
    }

    public class AdminDashboardStatsDto
    {
        public int TotalUsers { get; set; }
        public int TotalTournaments { get; set; }
        public int ActiveRaces { get; set; }
        public int TotalBets { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalPayout { get; set; }
        public decimal Profit { get; set; }
    }

    public class AdminRefereeAssignmentDto
    {
        public int RefereeId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string LicenseNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class AdminRaceRefereeDto
    {
        public long RaceId { get; set; }
        public string RaceName { get; set; } = string.Empty;
        public DateTime RaceDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int DistanceMeter { get; set; }
        public string RoundName { get; set; } = string.Empty;
        public string TournamentName { get; set; } = string.Empty;
        public List<AdminRefereeAssignmentDto> Referees { get; set; } = new List<AdminRefereeAssignmentDto>();
    }
}
