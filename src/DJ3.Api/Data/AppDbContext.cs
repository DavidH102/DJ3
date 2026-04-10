using DJ3.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DJ3.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasMany(e => e.Registrations)
                  .WithOne(r => r.Event)
                  .HasForeignKey(r => r.EventId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.OrganizerId);

            entity.Property(e => e.Tags);
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(r => r.Id);
        });
    }
}
