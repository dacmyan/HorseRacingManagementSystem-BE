using System;
using System.Collections.Generic;
using HorseRacing.Domain.Entities.Tournaments;

namespace HorseRacing.Application.Features.Public.DTOs;

public class TournamentDetailResponseDto : TournamentListResponseDto
{
    // Includes Rounds, which are mapped to a safe DTO to prevent lazy loading entity loops.
    public List<RoundDto> Rounds { get; set; } = new();
}

public class RoundDto
{
    public long RoundId { get; set; }
    public long TournamentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Status { get; set; } = string.Empty;
}
