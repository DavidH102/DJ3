using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DJ3.Api.Data;
using DJ3.Api.Models.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DJ3.Api.Tests.Endpoints;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all DbContext-related registrations to avoid provider conflicts
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });

        builder.UseEnvironment("Development");
    }
}

public class EventEndpointsV2Tests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventEndpointsV2Tests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---- Helper Methods ----

    private static async Task<string> GetAuthTokenAsync(HttpClient client, string role = "Admin")
    {
        var response = await client.PostAsJsonAsync("/api/v2/auth/token", new
        {
            username = "test",
            role
        });

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        return content.GetProperty("token").GetString()!;
    }

    private static void AddAuthHeader(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static object CreateEventBody()
    {
        return new
        {
            title = "Test Event",
            description = "Description",
            location = "Test Location",
            category = "Technology",
            organizerId = Guid.NewGuid(),
            organizerName = "Test Organizer",
            startDate = DateTime.UtcNow.AddDays(7),
            endDate = DateTime.UtcNow.AddDays(7).AddHours(1),
            maxCapacity = 100,
            tags = new[] { "test" }
        };
    }

    private async Task<EventResponse> CreateEventAndReturnAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v2/events", CreateEventBody());
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<EventResponse>(_jsonOptions);
        return created!;
    }

    private async Task<EventResponse> CreateAndPublishEventAsync(HttpClient client)
    {
        var created = await CreateEventAndReturnAsync(client);

        // Publish the event so it can accept registrations
        var publishResponse = await client.PutAsync($"/api/v2/events/{created.Id}/publish", null);
        publishResponse.EnsureSuccessStatusCode();

        var published = await publishResponse.Content.ReadFromJsonAsync<EventResponse>(_jsonOptions);
        return published!;
    }

    // ---- Tests ----

    [Fact]
    public async Task GetAllEvents_ReturnsOk_WhenNoEvents()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/events");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<EventResponse>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task CreateEvent_ReturnsCreated_WithValidAuth()
    {
        // Arrange
        var token = await GetAuthTokenAsync(_client);
        AddAuthHeader(_client, token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/events", CreateEventBody());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<EventResponse>(_jsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Test Event", created.Title);
        Assert.Equal("Test Location", created.Location);
        Assert.Equal("Technology", created.Category);
        Assert.Equal(100, created.MaxCapacity);
    }

    [Fact]
    public async Task CreateEvent_ReturnsUnauthorized_WithoutAuth()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/v2/events", CreateEventBody());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_ReturnsOk_WhenExists()
    {
        // Arrange
        var token = await GetAuthTokenAsync(_client);
        AddAuthHeader(_client, token);

        var created = await CreateEventAndReturnAsync(_client);

        // Act
        var response = await _client.GetAsync($"/api/v2/events/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var fetched = await response.Content.ReadFromJsonAsync<EventResponse>(_jsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Test Event", fetched.Title);
    }

    [Fact]
    public async Task GetEventById_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var randomId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v2/events/{randomId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_ReturnsOk()
    {
        // Arrange
        var token = await GetAuthTokenAsync(_client);
        AddAuthHeader(_client, token);

        var created = await CreateEventAndReturnAsync(_client);

        var updateBody = new
        {
            title = "Updated Event Title",
            description = "Updated Description"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v2/events/{created.Id}", updateBody);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<EventResponse>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Updated Event Title", updated.Title);
        Assert.Equal("Updated Description", updated.Description);
    }

    [Fact]
    public async Task DeleteEvent_RequiresAdminRole()
    {
        // Arrange - Create the event with Admin auth
        var adminToken = await GetAuthTokenAsync(_client, "Admin");
        AddAuthHeader(_client, adminToken);
        var created = await CreateEventAndReturnAsync(_client);

        // Switch to a non-admin (User) token
        var userToken = await GetAuthTokenAsync(_client, "User");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userToken);

        // Act
        var response = await _client.DeleteAsync($"/api/v2/events/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_ReturnsNoContent_WithAdmin()
    {
        // Arrange
        var token = await GetAuthTokenAsync(_client, "Admin");
        AddAuthHeader(_client, token);

        var created = await CreateEventAndReturnAsync(_client);

        // Act
        var response = await _client.DeleteAsync($"/api/v2/events/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it's actually gone
        var getResponse = await _client.GetAsync($"/api/v2/events/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task SearchEvents_ReturnsResults()
    {
        // Arrange
        var token = await GetAuthTokenAsync(_client);
        AddAuthHeader(_client, token);

        await CreateEventAndReturnAsync(_client);

        // Act
        var response = await _client.GetAsync("/api/v2/events/search?q=test");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<EventResponse>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task PublishEvent_ReturnsOk()
    {
        // Arrange - Create event with Admin token (Admin satisfies OrganizerOrAdmin policy)
        var adminToken = await GetAuthTokenAsync(_client, "Admin");
        AddAuthHeader(_client, adminToken);
        var created = await CreateEventAndReturnAsync(_client);

        // Use Organizer role for publishing
        var organizerToken = await GetAuthTokenAsync(_client, "Organizer");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", organizerToken);

        // Act
        var response = await _client.PutAsync($"/api/v2/events/{created.Id}/publish", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var published = await response.Content.ReadFromJsonAsync<EventResponse>(_jsonOptions);
        Assert.NotNull(published);
        Assert.True(published.IsPublished);
    }

    [Fact]
    public async Task RegisterForEvent_ReturnsCreated()
    {
        // Arrange
        var token = await GetAuthTokenAsync(_client);
        AddAuthHeader(_client, token);

        // Create and publish event (registration requires Published status)
        var published = await CreateAndPublishEventAsync(_client);

        var registerBody = new
        {
            userId = Guid.NewGuid(),
            userName = "Test User",
            userEmail = "test@example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v2/events/{published.Id}/register", registerBody);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var attendee = await response.Content.ReadFromJsonAsync<AttendeeResponse>(_jsonOptions);
        Assert.NotNull(attendee);
        Assert.Equal("Test User", attendee.UserName);
        Assert.Equal("test@example.com", attendee.UserEmail);
    }

    [Fact]
    public async Task GetEventStats_ReturnsStats()
    {
        // Arrange
        var token = await GetAuthTokenAsync(_client);
        AddAuthHeader(_client, token);

        var created = await CreateEventAndReturnAsync(_client);

        // Act
        var response = await _client.GetAsync($"/api/v2/events/{created.Id}/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stats = await response.Content.ReadFromJsonAsync<EventStatsResponse>(_jsonOptions);
        Assert.NotNull(stats);
        Assert.Equal(created.Id, stats.EventId);
        Assert.Equal(100, stats.MaxCapacity);
        Assert.Equal(0, stats.TotalRegistrations);
    }
}
