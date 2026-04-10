using DJ3.Api.Models;
using DJ3.Api.Tenant;
using Microsoft.EntityFrameworkCore;

namespace DJ3.Api.Data;

public class AppDbContext : DbContext
{
    private readonly Guid _currentTenantId;
    private readonly bool _tenantFilterEnabled;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _currentTenantId = Guid.Empty;
        _tenantFilterEnabled = false;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext) : base(options)
    {
        _currentTenantId = tenantContext.IsResolved ? tenantContext.TenantId : Guid.Empty;
        _tenantFilterEnabled = tenantContext.IsResolved;
    }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantEntity>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasMany(e => e.Registrations)
                  .WithOne(r => r.Event)
                  .HasForeignKey(r => r.EventId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.OrganizerId);
            entity.HasIndex(e => e.TenantId);

            entity.Property(e => e.Tags);

            entity.HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _currentTenantId);
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.TenantId);

            entity.HasQueryFilter(r => !_tenantFilterEnabled || r.TenantId == _currentTenantId);
        });
    }
}
