using DJ3.Api.Caching;
using DJ3.Api.Data;
using DJ3.Api.Models;
using DJ3.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DJ3.Api.Services;

public class EventService : IEventService
{
    private readonly AppDbContext _context;
    private readonly ICacheService _cache;
    private readonly ILogger<EventService> _logger;

    public EventService(AppDbContext context, ICacheService cache, ILogger<EventService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    private static EventResponse MapToResponse(Event e)
    {
        return new EventResponse
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            Location = e.Location,
            Category = e.Category,
            OrganizerId = e.OrganizerId,
            OrganizerName = e.OrganizerName,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            MaxCapacity = e.MaxCapacity,
            CurrentAttendees = e.CurrentAttendees,
            Status = e.Status,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            IsPublished = e.IsPublished,
            IsCancelled = e.IsCancelled,
            Tags = e.Tags
        };
    }

    private static async Task<PagedResult<EventResponse>> ApplyPagingAsync(IQueryable<Event> query, int page, int pageSize)
    {
        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<EventResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task InvalidateCachesAsync()
    {
        await _cache.RemoveByPrefixAsync(CacheKeys.EventPrefix);
        await _cache.RemoveByPrefixAsync(CacheKeys.EventsPrefix);
    }

    public async Task<PagedResult<EventResponse>> GetAllEventsAsync(int page = 1, int pageSize = 20)
    {
        var cacheKey = $"{CacheKeys.AllEvents}:p{page}:ps{pageSize}";
        var cached = await _cache.GetAsync<PagedResult<EventResponse>>(cacheKey);
        if (cached is not null)
            return cached;

        var query = _context.Events
            .Where(e => e.Status != EventStatus.Cancelled)
            .OrderBy(e => e.StartDate)
            .AsNoTracking();

        var result = await ApplyPagingAsync(query, page, pageSize);

        await _cache.SetAsync(cacheKey, result);
        return result;
    }

    public async Task<EventResponse?> GetEventByIdAsync(Guid id)
    {
        var cacheKey = CacheKeys.EventById(id);
        var cached = await _cache.GetAsync<EventResponse>(cacheKey);
        if (cached is not null)
            return cached;

        var ev = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
            return null;

        var response = MapToResponse(ev);
        await _cache.SetAsync(cacheKey, response);
        return response;
    }

    public async Task<EventResponse> CreateEventAsync(CreateEventRequest request)
    {
        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Location = request.Location,
            Category = request.Category,
            OrganizerId = request.OrganizerId,
            OrganizerName = request.OrganizerName,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MaxCapacity = request.MaxCapacity,
            CurrentAttendees = 0,
            Status = EventStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsPublished = false,
            IsCancelled = false,
            Tags = request.Tags ?? new List<string>(),
            Registrations = new List<Registration>()
        };

        _context.Events.Add(ev);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created event {EventId} with title '{Title}'", ev.Id, ev.Title);

        await InvalidateCachesAsync();

        return MapToResponse(ev);
    }

    public async Task<EventResponse?> UpdateEventAsync(Guid id, UpdateEventRequest request)
    {
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
            return null;

        if (request.Title is not null)
            ev.Title = request.Title;
        if (request.Description is not null)
            ev.Description = request.Description;
        if (request.Location is not null)
            ev.Location = request.Location;
        if (request.Category is not null)
            ev.Category = request.Category;
        if (request.StartDate.HasValue)
            ev.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue)
            ev.EndDate = request.EndDate.Value;
        if (request.MaxCapacity.HasValue)
            ev.MaxCapacity = request.MaxCapacity.Value;
        if (request.Tags is not null)
            ev.Tags = request.Tags;

        ev.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated event {EventId}", id);

        await InvalidateCachesAsync();

        return MapToResponse(ev);
    }

    public async Task<bool> DeleteEventAsync(Guid id)
    {
        var ev = await _context.Events.Include(e => e.Registrations).FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
            return false;

        if (ev.Registrations.Any())
        {
            _context.Registrations.RemoveRange(ev.Registrations);
        }

        _context.Events.Remove(ev);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted event {EventId}", id);

        await InvalidateCachesAsync();

        return true;
    }

    public async Task<PagedResult<EventResponse>> SearchEventsAsync(string query, int page = 1, int pageSize = 20)
    {
        var cacheKey = $"{CacheKeys.SearchResults(query)}:p{page}:ps{pageSize}";
        var cached = await _cache.GetAsync<PagedResult<EventResponse>>(cacheKey);
        if (cached is not null)
            return cached;

        var lowerQuery = query.ToLower();

        var dbQuery = _context.Events
            .Where(e =>
                e.Title.ToLower().Contains(lowerQuery) ||
                e.Description.ToLower().Contains(lowerQuery) ||
                e.Location.ToLower().Contains(lowerQuery) ||
                e.Category.ToLower().Contains(lowerQuery))
            .OrderBy(e => e.StartDate)
            .AsNoTracking();

        var result = await ApplyPagingAsync(dbQuery, page, pageSize);

        await _cache.SetAsync(cacheKey, result);
        return result;
    }

    public async Task<PagedResult<EventResponse>> GetUpcomingEventsAsync(int page = 1, int pageSize = 20)
    {
        var cacheKey = $"{CacheKeys.UpcomingEvents}:p{page}:ps{pageSize}";
        var cached = await _cache.GetAsync<PagedResult<EventResponse>>(cacheKey);
        if (cached is not null)
            return cached;

        var now = DateTime.UtcNow;

        var query = _context.Events
            .Where(e => e.StartDate > now && e.Status == EventStatus.Published)
            .OrderBy(e => e.StartDate)
            .AsNoTracking();

        var result = await ApplyPagingAsync(query, page, pageSize);

        await _cache.SetAsync(cacheKey, result);
        return result;
    }

    public async Task<PagedResult<EventResponse>> GetPastEventsAsync(int page = 1, int pageSize = 20)
    {
        var cacheKey = $"{CacheKeys.PastEvents}:p{page}:ps{pageSize}";
        var cached = await _cache.GetAsync<PagedResult<EventResponse>>(cacheKey);
        if (cached is not null)
            return cached;

        var now = DateTime.UtcNow;

        var query = _context.Events
            .Where(e => e.EndDate < now)
            .OrderByDescending(e => e.StartDate)
            .AsNoTracking();

        var result = await ApplyPagingAsync(query, page, pageSize);

        await _cache.SetAsync(cacheKey, result);
        return result;
    }

    public async Task<PagedResult<EventResponse>> GetEventsByCategoryAsync(string category, int page = 1, int pageSize = 20)
    {
        var cacheKey = $"{CacheKeys.EventsByCategory(category)}:p{page}:ps{pageSize}";
        var cached = await _cache.GetAsync<PagedResult<EventResponse>>(cacheKey);
        if (cached is not null)
            return cached;

        var lowerCategory = category.ToLower();

        var query = _context.Events
            .Where(e => e.Category.ToLower() == lowerCategory)
            .OrderBy(e => e.StartDate)
            .AsNoTracking();

        var result = await ApplyPagingAsync(query, page, pageSize);

        await _cache.SetAsync(cacheKey, result);
        return result;
    }

    public async Task<PagedResult<EventResponse>> GetEventsByOrganizerAsync(Guid organizerId, int page = 1, int pageSize = 20)
    {
        var cacheKey = $"{CacheKeys.EventsByOrganizer(organizerId)}:p{page}:ps{pageSize}";
        var cached = await _cache.GetAsync<PagedResult<EventResponse>>(cacheKey);
        if (cached is not null)
            return cached;

        var query = _context.Events
            .Where(e => e.OrganizerId == organizerId)
            .OrderBy(e => e.StartDate)
            .AsNoTracking();

        var result = await ApplyPagingAsync(query, page, pageSize);

        await _cache.SetAsync(cacheKey, result);
        return result;
    }

    public async Task<AttendeeResponse?> RegisterForEventAsync(Guid eventId, RegisterRequest request)
    {
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null)
            return null;

        if (ev.Status != EventStatus.Published)
            return null;

        if (ev.CurrentAttendees >= ev.MaxCapacity)
            return null;

        var alreadyRegistered = await _context.Registrations
            .AnyAsync(r => r.EventId == eventId && r.UserId == request.UserId);
        if (alreadyRegistered)
            return null;

        var registration = new Registration
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = request.UserId,
            UserName = request.UserName,
            UserEmail = request.UserEmail,
            RegisteredAt = DateTime.UtcNow
        };

        _context.Registrations.Add(registration);
        ev.CurrentAttendees++;
        ev.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} registered for event {EventId}", request.UserId, eventId);

        await InvalidateCachesAsync();

        return new AttendeeResponse
        {
            UserId = registration.UserId,
            UserName = registration.UserName,
            UserEmail = registration.UserEmail,
            RegisteredAt = registration.RegisteredAt
        };
    }

    public async Task<bool> UnregisterFromEventAsync(Guid eventId, Guid userId)
    {
        var registration = await _context.Registrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);
        if (registration is null)
            return false;

        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null)
            return false;

        _context.Registrations.Remove(registration);
        ev.CurrentAttendees--;
        ev.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} unregistered from event {EventId}", userId, eventId);

        await InvalidateCachesAsync();

        return true;
    }

    public async Task<List<AttendeeResponse>> GetEventAttendeesAsync(Guid eventId)
    {
        var cacheKey = CacheKeys.EventAttendees(eventId);
        var cached = await _cache.GetAsync<List<AttendeeResponse>>(cacheKey);
        if (cached is not null)
            return cached;

        var attendees = await _context.Registrations
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.RegisteredAt)
            .Select(r => new AttendeeResponse
            {
                UserId = r.UserId,
                UserName = r.UserName,
                UserEmail = r.UserEmail,
                RegisteredAt = r.RegisteredAt
            })
            .AsNoTracking()
            .ToListAsync();

        await _cache.SetAsync(cacheKey, attendees);
        return attendees;
    }

    public async Task<EventResponse?> PublishEventAsync(Guid id)
    {
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
            return null;

        ev.Status = EventStatus.Published;
        ev.IsPublished = true;
        ev.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Published event {EventId}", id);

        await InvalidateCachesAsync();

        return MapToResponse(ev);
    }

    public async Task<EventResponse?> CancelEventAsync(Guid id)
    {
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
            return null;

        ev.Status = EventStatus.Cancelled;
        ev.IsCancelled = true;
        ev.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Cancelled event {EventId}", id);

        await InvalidateCachesAsync();

        return MapToResponse(ev);
    }

    public async Task<EventStatsResponse?> GetEventStatsAsync(Guid id)
    {
        var cacheKey = CacheKeys.EventStats(id);
        var cached = await _cache.GetAsync<EventStatsResponse>(cacheKey);
        if (cached is not null)
            return cached;

        var ev = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
            return null;

        var totalRegistrations = await _context.Registrations
            .CountAsync(r => r.EventId == id);

        var stats = new EventStatsResponse
        {
            EventId = ev.Id,
            Title = ev.Title,
            TotalRegistrations = totalRegistrations,
            AvailableSpots = ev.MaxCapacity - ev.CurrentAttendees,
            MaxCapacity = ev.MaxCapacity,
            RegistrationRate = ev.MaxCapacity > 0
                ? (double)totalRegistrations / ev.MaxCapacity
                : 0
        };

        await _cache.SetAsync(cacheKey, stats);
        return stats;
    }

    public async Task<EventResponse?> DuplicateEventAsync(Guid id)
    {
        var ev = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
            return null;

        var duplicate = new Event
        {
            Id = Guid.NewGuid(),
            Title = $"{ev.Title} (Copy)",
            Description = ev.Description,
            Location = ev.Location,
            Category = ev.Category,
            OrganizerId = ev.OrganizerId,
            OrganizerName = ev.OrganizerName,
            StartDate = ev.StartDate,
            EndDate = ev.EndDate,
            MaxCapacity = ev.MaxCapacity,
            CurrentAttendees = 0,
            Status = EventStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsPublished = false,
            IsCancelled = false,
            Tags = ev.Tags != null ? new List<string>(ev.Tags) : new List<string>(),
            Registrations = new List<Registration>()
        };

        _context.Events.Add(duplicate);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Duplicated event {OriginalId} as {NewId}", id, duplicate.Id);

        await InvalidateCachesAsync();

        return MapToResponse(duplicate);
    }

    public async Task<EventResponse?> RescheduleEventAsync(Guid id, RescheduleRequest request)
    {
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null)
            return null;

        ev.StartDate = request.NewStartDate;
        ev.EndDate = request.NewEndDate;
        ev.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Rescheduled event {EventId} to {Start} - {End}", id, request.NewStartDate, request.NewEndDate);

        await InvalidateCachesAsync();

        return MapToResponse(ev);
    }
}
