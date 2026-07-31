using System;
using System.Collections.Generic;
using HorseRacing.Domain.Entities.Tournaments;

namespace HorseRacing.Application.Features.Public.DTOs;

public class TournamentListResponseDto
{
    public long TournamentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? RegistrationStartDate { get; set; }
    public DateTime? RegistrationEndDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CancelCount { get; set; }
    
    public int ApprovedRegistration { get; set; }
    public int QualifiedRegistration { get; set; }
    
    public List<TournamentPrizeDto> Prizes { get; set; } = new();
}

public class TournamentPrizeDto
{
    public long Id { get; set; }
    public int RankPosition { get; set; }
    public decimal Amount { get; set; }
}
