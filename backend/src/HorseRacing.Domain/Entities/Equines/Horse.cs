using System.Collections.Generic;

namespace HorseRacing.Domain.Entities;

public class Horse
{
    public long HorseId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime Age { get; set; }

    public string Gender { get; set; } = string.Empty;

    public string Breed { get; set; } = string.Empty;

    public string HealthStatus { get; set; } = "Healthy";

    public int OwnerId { get; set; }

    public AppUser? Owner { get; set; }

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public ICollection<HorseDocument> Documents { get; set; } = new List<HorseDocument>();

    public HorseStatistic? Statistic { get; set; }

    public decimal? AverageTime { get; set; }
    public decimal? RecentAverageTime { get; set; }
    public decimal? WinRate { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
