using System;
using System.Collections.Generic;

namespace HorseRacing.Application.Features.UserManagement.DTOs;

public class OwnerDashboardDto
{
    public int HorseCount { get; set; }
    public int RegistrationCount { get; set; }
    public int ActiveRaceCount { get; set; }
    public int UpcomingRaceCount { get; set; }
    public decimal TotalPrizeAmount { get; set; }
}

public class OwnerResultDto
{
    public long RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public string HorseName { get; set; } = string.Empty;
    public int FinishPosition { get; set; }
    public string FinishTime { get; set; } = string.Empty;
    public int Point { get; set; }
    public decimal PrizeAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int LaneNo { get; set; }
    public DateTime? RaceDate { get; set; }
}
