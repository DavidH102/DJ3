namespace DJ3.Api.Models.DTOs;

public class EventStatsResponse
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalRegistrations { get; set; }
    public int AvailableSpots { get; set; }
    public int MaxCapacity { get; set; }
    public double RegistrationRate { get; set; }
}
