namespace DJ3.Api.Tenant;

public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantName { get; }
    bool IsResolved { get; }
}
