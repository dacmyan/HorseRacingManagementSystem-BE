using System;

namespace HorseRacing.Application.Features.UserManagement.DTOs;

public class JockeyStatsDto
{
    public int TotalRaces { get; set; }
    public int Wins { get; set; }
    public int Top3 { get; set; }
    public int TotalPoints { get; set; }
    public int RankingPoint { get; set; }
}

public class JockeyViolationDto
{
    public long ViolationId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Penalty { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class JockeyAssignedHorseDto
{
    public long RaceEntryId { get; set; }
    public long RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateTime? RaceDate { get; set; }
    public long HorseId { get; set; }
    public string HorseName { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public int LaneNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? FinishPosition { get; set; }
    public decimal? FinishTime { get; set; }
}
