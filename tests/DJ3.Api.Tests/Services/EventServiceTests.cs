using DJ3.Api.Caching;
using DJ3.Api.Data;
using DJ3.Api.Models;
using DJ3.Api.Models.DTOs;
using DJ3.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DJ3.Api.Tests.Services;

public class EventServiceTests
{
    private readonly ICacheService _cache;
    private readonly ILogger<EventService> _logger;

    private static readonly Guid DefaultOrganizerId = Guid.NewGuid();

    public EventServiceTests()
    {
        _cache = Substitute.For<ICacheService>();
        _logger = Substitute.For<ILogger<EventService>>();

        // Default: cache miss for all GetAsync calls
        _cache.GetAsync<PagedResult<EventResponse>>(Arg.Any<string>())
            .Returns((PagedResult<EventResponse>?)null);
        _cache.GetAsync<EventResponse>(Arg.Any<string>())
            .Returns((EventResponse?)null);
        _cache.GetAsync<List<AttendeeResponse>>(Arg.Any<string>())
            .Returns((List<AttendeeResponse>?)null);
        _cache.GetAsync<EventStatsResponse>(Arg.Any<string>())
            .Returns((EventStatsResponse?)null);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static Event CreateSampleEvent(
        Guid? id = null,
        string title = "Sample Event",
        EventStatus status = EventStatus.Published,
        bool isPublished = true,
        bool isCancelled = false,
        int maxCapacity = 100,
        int currentAttendees = 0,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string category = "Tech",
        Guid? organizerId = null)
    {
        return new Event
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Description = "A sample event description",
            Location = "Sample Location",
            Category = category,
            OrganizerId = organizerId ?? DefaultOrganizerId,
            OrganizerName = "Test Organizer",
            StartDate = startDate ?? DateTime.UtcNow.AddDays(7),
            EndDate = endDate ?? DateTime.UtcNow.AddDays(7).AddHours(3),
            MaxCapacity = maxCapacity,
            CurrentAttendees = currentAttendees,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsPublished = isPublished,
            IsCancelled = isCancelled,
            Tags = new List<string> { "test", "sample" },
            Registrations = new List<Registration>()
        };
    }

    private static async Task<Event> SeedEventAsync(AppDbContext context, Event? ev = null)
    {
        var entity = ev ?? CreateSampleEvent();
        context.Events.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    private EventService CreateService(AppDbContext context)
    {
        return new EventService(context, _cache, _logger);
    }

    // -------------------------------------------------------
    // 1. GetAllEventsAsync_ReturnsNonCancelledEvents
    // -------------------------------------------------------
    [Fact]
    public async Task GetAllEventsAsync_ReturnsNonCancelledEvents()
    {
        using var context = CreateDbContext();
        await SeedEventAsync(context, CreateSampleEvent(title: "Active", status: EventStatus.Published));
        await SeedEventAsync(context, CreateSampleEvent(title: "Draft", status: EventStatus.Draft));
        await SeedEventAsync(context, CreateSampleEvent(title: "Cancelled", status: EventStatus.Cancelled, isCancelled: true));

        var service = CreateService(context);

        var result = await service.GetAllEventsAsync();

        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, e => e.Title == "Cancelled");
    }

    // -------------------------------------------------------
    // 2. GetAllEventsAsync_ReturnsCachedResult_WhenAvailable
    // -------------------------------------------------------
    [Fact]
    public async Task GetAllEventsAsync_ReturnsCachedResult_WhenAvailable()
    {
        using var context = CreateDbContext();
        var cachedResult = new PagedResult<EventResponse>
        {
            Items = new List<EventResponse>
            {
                new EventResponse
                {
                    Id = Guid.NewGuid(),
                    Title = "Cached Event",
                    Description = "From cache",
                    Location = "Cache City",
                    Category = "Cache",
                    OrganizerId = DefaultOrganizerId,
                    OrganizerName = "Cached Organizer",
                    StartDate = DateTime.UtcNow.AddDays(1),
                    EndDate = DateTime.UtcNow.AddDays(1).AddHours(2),
                    MaxCapacity = 50,
                    CurrentAttendees = 10,
                    Status = EventStatus.Published,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsPublished = true,
                    IsCancelled = false,
                    Tags = new List<string> { "cached" }
                }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _cache.GetAsync<PagedResult<EventResponse>>(Arg.Any<string>())
            .Returns(cachedResult);

        var service = CreateService(context);

        var result = await service.GetAllEventsAsync();

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Cached Event", result.Items[0].Title);
    }

    // -------------------------------------------------------
    // 3. GetEventByIdAsync_ReturnsEvent_WhenExists
    // -------------------------------------------------------
    [Fact]
    public async Task GetEventByIdAsync_ReturnsEvent_WhenExists()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context);
        var service = CreateService(context);

        var result = await service.GetEventByIdAsync(ev.Id);

        Assert.NotNull(result);
        Assert.Equal(ev.Id, result.Id);
        Assert.Equal(ev.Title, result.Title);
    }

    // -------------------------------------------------------
    // 4. GetEventByIdAsync_ReturnsNull_WhenNotExists
    // -------------------------------------------------------
    [Fact]
    public async Task GetEventByIdAsync_ReturnsNull_WhenNotExists()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        var result = await service.GetEventByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // -------------------------------------------------------
    // 5. CreateEventAsync_CreatesAndReturnsEvent
    // -------------------------------------------------------
    [Fact]
    public async Task CreateEventAsync_CreatesAndReturnsEvent()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        var request = new CreateEventRequest
        {
            Title = "New Event",
            Description = "Description",
            Location = "Location",
            Category = "Tech",
            OrganizerId = Guid.NewGuid(),
            OrganizerName = "Organizer",
            StartDate = DateTime.UtcNow.AddDays(5),
            EndDate = DateTime.UtcNow.AddDays(5).AddHours(2),
            MaxCapacity = 200,
            Tags = new List<string> { "new" }
        };

        var result = await service.CreateEventAsync(request);

        Assert.NotNull(result);
        Assert.Equal("New Event", result.Title);
        Assert.Equal(EventStatus.Draft, result.Status);
        Assert.Equal(0, result.CurrentAttendees);
        Assert.False(result.IsPublished);
        Assert.False(result.IsCancelled);

        var dbEvent = await context.Events.FirstOrDefaultAsync(e => e.Id == result.Id);
        Assert.NotNull(dbEvent);
    }

    // -------------------------------------------------------
    // 6. CreateEventAsync_InvalidatesCaches
    // -------------------------------------------------------
    [Fact]
    public async Task CreateEventAsync_InvalidatesCaches()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        var request = new CreateEventRequest
        {
            Title = "Cache Test Event",
            Description = "Desc",
            Location = "Loc",
            Category = "Cat",
            OrganizerId = Guid.NewGuid(),
            OrganizerName = "Org",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(1).AddHours(1),
            MaxCapacity = 50,
            Tags = new List<string>()
        };

        await service.CreateEventAsync(request);

        await _cache.Received().RemoveByPrefixAsync(CacheKeys.EventPrefix);
        await _cache.Received().RemoveByPrefixAsync(CacheKeys.EventsPrefix);
    }

    // -------------------------------------------------------
    // 7. UpdateEventAsync_UpdatesOnlyProvidedFields
    // -------------------------------------------------------
    [Fact]
    public async Task UpdateEventAsync_UpdatesOnlyProvidedFields()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context, CreateSampleEvent(title: "Original Title"));
        var service = CreateService(context);

        var request = new UpdateEventRequest
        {
            Title = "Updated Title"
            // Other fields are null so they should not be updated
        };

        var result = await service.UpdateEventAsync(ev.Id, request);

        Assert.NotNull(result);
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal("Sample Location", result.Location); // Unchanged
        Assert.Equal("A sample event description", result.Description); // Unchanged
    }

    // -------------------------------------------------------
    // 8. UpdateEventAsync_ReturnsNull_WhenNotExists
    // -------------------------------------------------------
    [Fact]
    public async Task UpdateEventAsync_ReturnsNull_WhenNotExists()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        var request = new UpdateEventRequest { Title = "Nothing" };

        var result = await service.UpdateEventAsync(Guid.NewGuid(), request);

        Assert.Null(result);
    }

    // -------------------------------------------------------
    // 9. DeleteEventAsync_RemovesEventAndRegistrations
    // -------------------------------------------------------
    [Fact]
    public async Task DeleteEventAsync_RemovesEventAndRegistrations()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context);

        var registration = new Registration
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            UserId = Guid.NewGuid(),
            UserName = "Test User",
            UserEmail = "test@example.com",
            RegisteredAt = DateTime.UtcNow
        };
        context.Registrations.Add(registration);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.DeleteEventAsync(ev.Id);

        Assert.True(result);
        Assert.Empty(await context.Events.Where(e => e.Id == ev.Id).ToListAsync());
        Assert.Empty(await context.Registrations.Where(r => r.EventId == ev.Id).ToListAsync());
    }

    // -------------------------------------------------------
    // 10. DeleteEventAsync_ReturnsFalse_WhenNotExists
    // -------------------------------------------------------
    [Fact]
    public async Task DeleteEventAsync_ReturnsFalse_WhenNotExists()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        var result = await service.DeleteEventAsync(Guid.NewGuid());

        Assert.False(result);
    }

    // -------------------------------------------------------
    // 11. SearchEventsAsync_FindsByTitle
    // -------------------------------------------------------
    [Fact]
    public async Task SearchEventsAsync_FindsByTitle()
    {
        using var context = CreateDbContext();
        await SeedEventAsync(context, CreateSampleEvent(title: "Kubernetes Workshop"));
        await SeedEventAsync(context, CreateSampleEvent(title: "React Meetup"));
        var service = CreateService(context);

        var result = await service.SearchEventsAsync("Kubernetes");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Kubernetes Workshop", result.Items[0].Title);
    }

    // -------------------------------------------------------
    // 12. SearchEventsAsync_IsCaseInsensitive
    // -------------------------------------------------------
    [Fact]
    public async Task SearchEventsAsync_IsCaseInsensitive()
    {
        using var context = CreateDbContext();
        await SeedEventAsync(context, CreateSampleEvent(title: "Docker Training"));
        var service = CreateService(context);

        var result = await service.SearchEventsAsync("docker training");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Docker Training", result.Items[0].Title);
    }

    // -------------------------------------------------------
    // 13. GetUpcomingEventsAsync_ReturnsOnlyPublishedFutureEvents
    // -------------------------------------------------------
    [Fact]
    public async Task GetUpcomingEventsAsync_ReturnsOnlyPublishedFutureEvents()
    {
        using var context = CreateDbContext();

        // Published future event
        await SeedEventAsync(context, CreateSampleEvent(
            title: "Future Published",
            status: EventStatus.Published,
            isPublished: true,
            startDate: DateTime.UtcNow.AddDays(10)));

        // Draft future event (should be excluded)
        await SeedEventAsync(context, CreateSampleEvent(
            title: "Future Draft",
            status: EventStatus.Draft,
            isPublished: false,
            startDate: DateTime.UtcNow.AddDays(10)));

        // Published past event (should be excluded)
        await SeedEventAsync(context, CreateSampleEvent(
            title: "Past Published",
            status: EventStatus.Published,
            isPublished: true,
            startDate: DateTime.UtcNow.AddDays(-5)));

        var service = CreateService(context);

        var result = await service.GetUpcomingEventsAsync();

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Future Published", result.Items[0].Title);
    }

    // -------------------------------------------------------
    // 14. GetPastEventsAsync_ReturnsOnlyPastEvents
    // -------------------------------------------------------
    [Fact]
    public async Task GetPastEventsAsync_ReturnsOnlyPastEvents()
    {
        using var context = CreateDbContext();

        // Past event
        await SeedEventAsync(context, CreateSampleEvent(
            title: "Past Event",
            startDate: DateTime.UtcNow.AddDays(-10),
            endDate: DateTime.UtcNow.AddDays(-9)));

        // Future event (should be excluded)
        await SeedEventAsync(context, CreateSampleEvent(
            title: "Future Event",
            startDate: DateTime.UtcNow.AddDays(5),
            endDate: DateTime.UtcNow.AddDays(6)));

        var service = CreateService(context);

        var result = await service.GetPastEventsAsync();

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Past Event", result.Items[0].Title);
    }

    // -------------------------------------------------------
    // 15. GetEventsByCategoryAsync_FiltersByCategory
    // -------------------------------------------------------
    [Fact]
    public async Task GetEventsByCategoryAsync_FiltersByCategory()
    {
        using var context = CreateDbContext();
        await SeedEventAsync(context, CreateSampleEvent(title: "Tech Event", category: "Technology"));
        await SeedEventAsync(context, CreateSampleEvent(title: "Music Event", category: "Music"));
        var service = CreateService(context);

        var result = await service.GetEventsByCategoryAsync("Technology");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Tech Event", result.Items[0].Title);
    }

    // -------------------------------------------------------
    // 16. GetEventsByOrganizerAsync_FiltersByOrganizer
    // -------------------------------------------------------
    [Fact]
    public async Task GetEventsByOrganizerAsync_FiltersByOrganizer()
    {
        using var context = CreateDbContext();
        var orgId = Guid.NewGuid();
        await SeedEventAsync(context, CreateSampleEvent(title: "Org Event", organizerId: orgId));
        await SeedEventAsync(context, CreateSampleEvent(title: "Other Event", organizerId: Guid.NewGuid()));
        var service = CreateService(context);

        var result = await service.GetEventsByOrganizerAsync(orgId);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Org Event", result.Items[0].Title);
    }

    // -------------------------------------------------------
    // 17. RegisterForEventAsync_SuccessfullyRegisters
    // -------------------------------------------------------
    [Fact]
    public async Task RegisterForEventAsync_SuccessfullyRegisters()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context, CreateSampleEvent(
            status: EventStatus.Published,
            isPublished: true,
            maxCapacity: 100,
            currentAttendees: 0));
        var service = CreateService(context);

        var request = new RegisterRequest
        {
            UserId = Guid.NewGuid(),
            UserName = "John Doe",
            UserEmail = "john@example.com"
        };

        var result = await service.RegisterForEventAsync(ev.Id, request);

        Assert.NotNull(result);
        Assert.Equal("John Doe", result.UserName);
        Assert.Equal("john@example.com", result.UserEmail);

        var updatedEvent = await context.Events.FindAsync(ev.Id);
        Assert.Equal(1, updatedEvent!.CurrentAttendees);
    }

    // -------------------------------------------------------
    // 18. RegisterForEventAsync_ReturnsNull_WhenEventFull
    // -------------------------------------------------------
    [Fact]
    public async Task RegisterForEventAsync_ReturnsNull_WhenEventFull()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context, CreateSampleEvent(
            status: EventStatus.Published,
            isPublished: true,
            maxCapacity: 1,
            currentAttendees: 1));
        var service = CreateService(context);

        var request = new RegisterRequest
        {
            UserId = Guid.NewGuid(),
            UserName = "Late Comer",
            UserEmail = "late@example.com"
        };

        var result = await service.RegisterForEventAsync(ev.Id, request);

        Assert.Null(result);
    }

    // -------------------------------------------------------
    // 19. RegisterForEventAsync_ReturnsNull_WhenAlreadyRegistered
    // -------------------------------------------------------
    [Fact]
    public async Task RegisterForEventAsync_ReturnsNull_WhenAlreadyRegistered()
    {
        using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var ev = await SeedEventAsync(context, CreateSampleEvent(
            status: EventStatus.Published,
            isPublished: true,
            maxCapacity: 100,
            currentAttendees: 1));

        context.Registrations.Add(new Registration
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            UserId = userId,
            UserName = "Existing User",
            UserEmail = "existing@example.com",
            RegisteredAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var request = new RegisterRequest
        {
            UserId = userId,
            UserName = "Existing User",
            UserEmail = "existing@example.com"
        };

        var result = await service.RegisterForEventAsync(ev.Id, request);

        Assert.Null(result);
    }

    // -------------------------------------------------------
    // 20. RegisterForEventAsync_ReturnsNull_WhenEventNotPublished
    // -------------------------------------------------------
    [Fact]
    public async Task RegisterForEventAsync_ReturnsNull_WhenEventNotPublished()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context, CreateSampleEvent(
            status: EventStatus.Draft,
            isPublished: false,
            maxCapacity: 100,
            currentAttendees: 0));
        var service = CreateService(context);

        var request = new RegisterRequest
        {
            UserId = Guid.NewGuid(),
            UserName = "Eager User",
            UserEmail = "eager@example.com"
        };

        var result = await service.RegisterForEventAsync(ev.Id, request);

        Assert.Null(result);
    }

    // -------------------------------------------------------
    // 21. UnregisterFromEventAsync_RemovesRegistration
    // -------------------------------------------------------
    [Fact]
    public async Task UnregisterFromEventAsync_RemovesRegistration()
    {
        using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var ev = await SeedEventAsync(context, CreateSampleEvent(
            status: EventStatus.Published,
            isPublished: true,
            currentAttendees: 1));

        context.Registrations.Add(new Registration
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            UserId = userId,
            UserName = "Leaving User",
            UserEmail = "leaving@example.com",
            RegisteredAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.UnregisterFromEventAsync(ev.Id, userId);

        Assert.True(result);

        var registration = await context.Registrations
            .FirstOrDefaultAsync(r => r.EventId == ev.Id && r.UserId == userId);
        Assert.Null(registration);

        var updatedEvent = await context.Events.FindAsync(ev.Id);
        Assert.Equal(0, updatedEvent!.CurrentAttendees);
    }

    // -------------------------------------------------------
    // 22. UnregisterFromEventAsync_ReturnsFalse_WhenNotRegistered
    // -------------------------------------------------------
    [Fact]
    public async Task UnregisterFromEventAsync_ReturnsFalse_WhenNotRegistered()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context);
        var service = CreateService(context);

        var result = await service.UnregisterFromEventAsync(ev.Id, Guid.NewGuid());

        Assert.False(result);
    }

    // -------------------------------------------------------
    // 23. GetEventAttendeesAsync_ReturnsAttendees
    // -------------------------------------------------------
    [Fact]
    public async Task GetEventAttendeesAsync_ReturnsAttendees()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context, CreateSampleEvent(currentAttendees: 2));

        context.Registrations.AddRange(
            new Registration
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                UserId = Guid.NewGuid(),
                UserName = "Alice",
                UserEmail = "alice@example.com",
                RegisteredAt = DateTime.UtcNow.AddHours(-2)
            },
            new Registration
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                UserId = Guid.NewGuid(),
                UserName = "Bob",
                UserEmail = "bob@example.com",
                RegisteredAt = DateTime.UtcNow.AddHours(-1)
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.GetEventAttendeesAsync(ev.Id);

        Assert.Equal(2, result.Count);
        Assert.Equal("Alice", result[0].UserName);
        Assert.Equal("Bob", result[1].UserName);
    }

    // -------------------------------------------------------
    // 24. PublishEventAsync_SetsPublishedStatus
    // -------------------------------------------------------
    [Fact]
    public async Task PublishEventAsync_SetsPublishedStatus()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context, CreateSampleEvent(
            status: EventStatus.Draft,
            isPublished: false));
        var service = CreateService(context);

        var result = await service.PublishEventAsync(ev.Id);

        Assert.NotNull(result);
        Assert.Equal(EventStatus.Published, result.Status);
        Assert.True(result.IsPublished);

        var dbEvent = await context.Events.FindAsync(ev.Id);
        Assert.Equal(EventStatus.Published, dbEvent!.Status);
        Assert.True(dbEvent.IsPublished);
    }

    // -------------------------------------------------------
    // 25. CancelEventAsync_SetsCancelledStatus
    // -------------------------------------------------------
    [Fact]
    public async Task CancelEventAsync_SetsCancelledStatus()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context, CreateSampleEvent(
            status: EventStatus.Published,
            isPublished: true));
        var service = CreateService(context);

        var result = await service.CancelEventAsync(ev.Id);

        Assert.NotNull(result);
        Assert.Equal(EventStatus.Cancelled, result.Status);
        Assert.True(result.IsCancelled);

        var dbEvent = await context.Events.FindAsync(ev.Id);
        Assert.Equal(EventStatus.Cancelled, dbEvent!.Status);
        Assert.True(dbEvent.IsCancelled);
    }

    // -------------------------------------------------------
    // 26. GetEventStatsAsync_ReturnsCorrectStats
    // -------------------------------------------------------
    [Fact]
    public async Task GetEventStatsAsync_ReturnsCorrectStats()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context, CreateSampleEvent(
            maxCapacity: 100,
            currentAttendees: 25));

        // Add 25 registrations to match CurrentAttendees
        for (int i = 0; i < 25; i++)
        {
            context.Registrations.Add(new Registration
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                UserId = Guid.NewGuid(),
                UserName = $"User {i}",
                UserEmail = $"user{i}@example.com",
                RegisteredAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.GetEventStatsAsync(ev.Id);

        Assert.NotNull(result);
        Assert.Equal(ev.Id, result.EventId);
        Assert.Equal(ev.Title, result.Title);
        Assert.Equal(25, result.TotalRegistrations);
        Assert.Equal(75, result.AvailableSpots);
        Assert.Equal(100, result.MaxCapacity);
        Assert.Equal(0.25, result.RegistrationRate, 2);
    }

    // -------------------------------------------------------
    // 27. GetEventStatsAsync_ReturnsNull_WhenNotExists
    // -------------------------------------------------------
    [Fact]
    public async Task GetEventStatsAsync_ReturnsNull_WhenNotExists()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        var result = await service.GetEventStatsAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // -------------------------------------------------------
    // 28. DuplicateEventAsync_CreatesCopyWithNewId
    // -------------------------------------------------------
    [Fact]
    public async Task DuplicateEventAsync_CreatesCopyWithNewId()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context, CreateSampleEvent(
            title: "Original Event",
            category: "Tech",
            maxCapacity: 200));
        var service = CreateService(context);

        var result = await service.DuplicateEventAsync(ev.Id);

        Assert.NotNull(result);
        Assert.NotEqual(ev.Id, result.Id);
        Assert.Equal("Original Event (Copy)", result.Title);
        Assert.Equal("Tech", result.Category);
        Assert.Equal(200, result.MaxCapacity);
        Assert.Equal(0, result.CurrentAttendees);
        Assert.Equal(EventStatus.Draft, result.Status);
        Assert.False(result.IsPublished);
        Assert.False(result.IsCancelled);

        // Verify two events exist in the database
        var eventCount = await context.Events.CountAsync();
        Assert.Equal(2, eventCount);
    }

    // -------------------------------------------------------
    // 29. DuplicateEventAsync_ReturnsNull_WhenNotExists
    // -------------------------------------------------------
    [Fact]
    public async Task DuplicateEventAsync_ReturnsNull_WhenNotExists()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        var result = await service.DuplicateEventAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // -------------------------------------------------------
    // 30. RescheduleEventAsync_UpdatesDates
    // -------------------------------------------------------
    [Fact]
    public async Task RescheduleEventAsync_UpdatesDates()
    {
        using var context = CreateDbContext();
        var ev = await SeedEventAsync(context);
        var service = CreateService(context);

        var newStart = DateTime.UtcNow.AddDays(30);
        var newEnd = DateTime.UtcNow.AddDays(30).AddHours(4);

        var request = new RescheduleRequest
        {
            NewStartDate = newStart,
            NewEndDate = newEnd
        };

        var result = await service.RescheduleEventAsync(ev.Id, request);

        Assert.NotNull(result);
        Assert.Equal(newStart, result.StartDate);
        Assert.Equal(newEnd, result.EndDate);

        var dbEvent = await context.Events.FindAsync(ev.Id);
        Assert.Equal(newStart, dbEvent!.StartDate);
        Assert.Equal(newEnd, dbEvent.EndDate);
    }
}
