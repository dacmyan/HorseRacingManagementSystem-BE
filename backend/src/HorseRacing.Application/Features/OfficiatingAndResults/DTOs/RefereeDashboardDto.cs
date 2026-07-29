using System;
using System.Collections.Generic;

namespace HorseRacing.Application.Features.OfficiatingAndResults.DTOs;

public class RefereeDashboardDto
{
    public int AssignedRaceCount { get; set; }
    public int PendingReportCount { get; set; }
    public int CompletedReportCount { get; set; }
    public int ViolationsCreatedCount { get; set; }
    public List<DashboardAssignedRaceDto> AssignedRaces { get; set; } = new();
}

public class DashboardAssignedRaceDto
{
    public long RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateTime? RaceDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? TournamentId { get; set; }
}

public class HorseCheckDto
{
    public long RaceEntryId { get; set; }
    public long HorseId { get; set; }
    public string HorseName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string JockeyName { get; set; } = string.Empty;
    public int LaneNo { get; set; }
    public string MedicalStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
