using System;

namespace HorseRacing.Application.Features.Public.DTOs;

public class LiveRaceResponseDto
{
    public long RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public string Status { get; set; } = string.Empty;
}
