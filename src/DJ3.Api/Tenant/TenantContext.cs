namespace DJ3.Api.Tenant;

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
}
