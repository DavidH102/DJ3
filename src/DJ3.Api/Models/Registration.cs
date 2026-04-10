using DJ3.Api.Tenant;

namespace DJ3.Api.Models;

public class Registration : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; }

    // Navigation
    public Event Event { get; set; } = null!;
}
