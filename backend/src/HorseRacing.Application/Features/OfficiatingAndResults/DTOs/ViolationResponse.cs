namespace HorseRacing.Application.Features.OfficiatingAndResults.DTOs;

public class ViolationResponse
{
    public int ViolationId { get; set; }
    public long RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Penalty { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long? RefereeId { get; set; }
    public string? RefereeName { get; set; }
}
