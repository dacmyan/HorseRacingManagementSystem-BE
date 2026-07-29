using System;

namespace HorseRacing.Application.Features.OfficiatingAndResults.DTOs;

public class AssignedRaceDto
{
    public long AssignmentId { get; set; }
    public long RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public DateTime? RaceDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
