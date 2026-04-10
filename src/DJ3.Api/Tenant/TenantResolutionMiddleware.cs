namespace DJ3.Api.Tenant;

using DJ3.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, AppDbContext dbContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip tenant resolution for auth, tenant admin, and OpenAPI endpoints
        if (path.StartsWith("/api/v2/auth/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v2/tenants", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/openapi/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/openapi", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        Guid? tenantId = null;
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

        // Strategy 1: JWT claim (always checked first)
        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var claimTenantId))
        {
            tenantId = claimTenantId;
        }

        // Strategy 2: Header fallback — only allowed for authenticated requests
        if (tenantId is null && isAuthenticated &&
            context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue))
        {
            if (Guid.TryParse(headerValue.FirstOrDefault(), out var headerTenantId))
            {
                tenantId = headerTenantId;
            }
        }

        // Strategy 3: Header for unauthenticated requests (public endpoints)
        // Still require the header but validate tenant exists
        if (tenantId is null && !isAuthenticated &&
            context.Request.Headers.TryGetValue("X-Tenant-Id", out var publicHeaderValue))
        {
            if (Guid.TryParse(publicHeaderValue.FirstOrDefault(), out var publicTenantId))
            {
                tenantId = publicTenantId;
            }
        }

        if (tenantId is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant resolution failed. Provide tenant_id in JWT or X-Tenant-Id header." });
            return;
        }

        // Validate tenant exists and is active
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value && t.IsActive);

        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant not found or inactive." });
            return;
        }

        tenantContext.TenantId = tenant.Id;
        tenantContext.TenantName = tenant.Name;
        tenantContext.IsResolved = true;

        _logger.LogDebug("Resolved tenant {TenantId} ({TenantName})", tenant.Id, tenant.Name);

        await _next(context);
    }
}
