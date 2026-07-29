using System;

namespace HorseRacing.Application.Features.TournamentAndRacing.DTOs;

public class ExtendRegistrationResponse
{
    public string Message { get; set; } = string.Empty;
    public DateTime? NewRegistrationEndDate { get; set; }
    public int QualifiedHorses { get; set; }
}
