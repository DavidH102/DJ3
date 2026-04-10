using DJ3.Api.Auth;
using DJ3.Api.Caching;
using DJ3.Api.Data;
using DJ3.Api.Endpoints;
using DJ3.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOpenApi();

// EF Core with SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=dj3.db"));

// Caching
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// Service layer
builder.Services.AddScoped<IEventService, EventService>();

// Auth
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

// Ensure DB is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapAuthEndpoints();
app.MapEventEndpointsV2();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
