namespace DJ3.Api.Tenant;

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; internal set; }
    public string TenantName { get; internal set; } = string.Empty;
    public bool IsResolved { get; internal set; }
}
