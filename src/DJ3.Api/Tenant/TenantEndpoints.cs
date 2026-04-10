namespace DJ3.Api.Tenant;

using DJ3.Api.Data;
using Microsoft.EntityFrameworkCore;

public static class TenantEndpoints
{
    public record CreateTenantRequest(string Name, string Slug);
    public record TenantResponse(Guid Id, string Name, string Slug, bool IsActive, DateTime CreatedAt);

    public static WebApplication MapTenantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v2/tenants")
            .WithTags("Tenants");

        // GET / - List all tenants (Admin only)
        group.MapGet("/", async (AppDbContext db) =>
        {
            var tenants = await db.Tenants.AsNoTracking()
                .Select(t => new TenantResponse(t.Id, t.Name, t.Slug, t.IsActive, t.CreatedAt))
                .ToListAsync();
            return Results.Ok(tenants);
        })
        .RequireAuthorization("AdminOnly")
        .WithName("GetAllTenants");

        // POST / - Create a new tenant (Admin only)
        group.MapPost("/", async (AppDbContext db, CreateTenantRequest request) =>
        {
            var exists = await db.Tenants.AnyAsync(t => t.Slug == request.Slug);
            if (exists)
                return Results.Conflict("Tenant with this slug already exists.");

            var tenant = new TenantEntity
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Slug = request.Slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v2/tenants/{tenant.Id}",
                new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive, tenant.CreatedAt));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("CreateTenant");

        // GET /{id:guid} - Get tenant by ID (Admin only)
        group.MapGet("/{id:guid}", async (AppDbContext db, Guid id) =>
        {
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (tenant is null)
                return Results.NotFound();

            return Results.Ok(new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive, tenant.CreatedAt));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("GetTenantById");

        // PUT /{id:guid}/deactivate - Deactivate a tenant (Admin only)
        group.MapPut("/{id:guid}/deactivate", async (AppDbContext db, Guid id) =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant is null)
                return Results.NotFound();

            tenant.IsActive = false;
            await db.SaveChangesAsync();

            return Results.Ok(new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive, tenant.CreatedAt));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("DeactivateTenant");

        // PUT /{id:guid}/activate - Activate a tenant (Admin only)
        group.MapPut("/{id:guid}/activate", async (AppDbContext db, Guid id) =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant is null)
                return Results.NotFound();

            tenant.IsActive = true;
            await db.SaveChangesAsync();

            return Results.Ok(new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive, tenant.CreatedAt));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("ActivateTenant");

        return app;
    }
}
