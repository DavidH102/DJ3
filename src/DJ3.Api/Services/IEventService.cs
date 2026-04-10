namespace DJ3.Api.Services;

using DJ3.Api.Models;
using DJ3.Api.Models.DTOs;

public interface IEventService
{
    // CRUD
    Task<PagedResult<EventResponse>> GetAllEventsAsync(int page = 1, int pageSize = 20);
    Task<EventResponse?> GetEventByIdAsync(Guid id);
    Task<EventResponse> CreateEventAsync(CreateEventRequest request);
    Task<EventResponse?> UpdateEventAsync(Guid id, UpdateEventRequest request);
    Task<bool> DeleteEventAsync(Guid id);

    // Queries
    Task<PagedResult<EventResponse>> SearchEventsAsync(string query, int page = 1, int pageSize = 20);
    Task<PagedResult<EventResponse>> GetUpcomingEventsAsync(int page = 1, int pageSize = 20);
    Task<PagedResult<EventResponse>> GetPastEventsAsync(int page = 1, int pageSize = 20);
    Task<PagedResult<EventResponse>> GetEventsByCategoryAsync(string category, int page = 1, int pageSize = 20);
    Task<PagedResult<EventResponse>> GetEventsByOrganizerAsync(Guid organizerId, int page = 1, int pageSize = 20);

    // Registration
    Task<AttendeeResponse?> RegisterForEventAsync(Guid eventId, RegisterRequest request);
    Task<bool> UnregisterFromEventAsync(Guid eventId, Guid userId);
    Task<List<AttendeeResponse>> GetEventAttendeesAsync(Guid eventId);

    // Actions
    Task<EventResponse?> PublishEventAsync(Guid id);
    Task<EventResponse?> CancelEventAsync(Guid id);
    Task<EventStatsResponse?> GetEventStatsAsync(Guid id);
    Task<EventResponse?> DuplicateEventAsync(Guid id);
    Task<EventResponse?> RescheduleEventAsync(Guid id, RescheduleRequest request);
}
