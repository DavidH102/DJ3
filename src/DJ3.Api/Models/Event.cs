using DJ3.Api.Tenant;

namespace DJ3.Api.Models;

public class Event : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public Guid OrganizerId { get; set; }
    public string OrganizerName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int MaxCapacity { get; set; }
    public int CurrentAttendees { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsPublished { get; set; }
    public bool IsCancelled { get; set; }

    public List<string> Tags { get; set; } = [];

    // Navigation
    public List<Registration> Registrations { get; set; } = [];
}
