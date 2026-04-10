using DJ3.Api.Models.DTOs;
using DJ3.Api.Services;

namespace DJ3.Api.Endpoints;

public static class EventEndpointsV2
{
    public static WebApplication MapEventEndpointsV2(this WebApplication app)
    {
        var group = app.MapGroup("/api/v2/events")
            .WithTags("Events");

        // 1. GET / - GetAllEvents
        group.MapGet("/", async (IEventService service, int page = 1, int pageSize = 20) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);
            var result = await service.GetAllEventsAsync(page, pageSize);
            return Results.Ok(result);
        })
        .WithName("GetAllEvents")
        .Produces<PagedResult<EventResponse>>(StatusCodes.Status200OK);

        // 2. GET /{id:guid} - GetEventById
        group.MapGet("/{id:guid}", async (IEventService service, Guid id) =>
        {
            var result = await service.GetEventByIdAsync(id);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetEventById")
        .Produces<EventResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 3. POST / - CreateEvent
        group.MapPost("/", async (IEventService service, CreateEventRequest request) =>
        {
            var result = await service.CreateEventAsync(request);
            return Results.Created($"/api/v2/events/{result.Id}", result);
        })
        .RequireAuthorization()
        .WithName("CreateEvent")
        .Produces<EventResponse>(StatusCodes.Status201Created);

        // 4. PUT /{id:guid} - UpdateEvent
        group.MapPut("/{id:guid}", async (IEventService service, Guid id, UpdateEventRequest request) =>
        {
            var result = await service.UpdateEventAsync(id, request);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization()
        .WithName("UpdateEvent")
        .Produces<EventResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 5. DELETE /{id:guid} - DeleteEvent
        group.MapDelete("/{id:guid}", async (IEventService service, Guid id) =>
        {
            var deleted = await service.DeleteEventAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization("AdminOnly")
        .WithName("DeleteEvent")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // 6. GET /search - SearchEvents
        group.MapGet("/search", async (IEventService service, string q, int page = 1, int pageSize = 20) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);
            var result = await service.SearchEventsAsync(q, page, pageSize);
            return Results.Ok(result);
        })
        .WithName("SearchEvents")
        .Produces<PagedResult<EventResponse>>(StatusCodes.Status200OK);

        // 7. GET /upcoming - GetUpcomingEvents
        group.MapGet("/upcoming", async (IEventService service, int page = 1, int pageSize = 20) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);
            var result = await service.GetUpcomingEventsAsync(page, pageSize);
            return Results.Ok(result);
        })
        .WithName("GetUpcomingEvents")
        .Produces<PagedResult<EventResponse>>(StatusCodes.Status200OK);

        // 8. GET /past - GetPastEvents
        group.MapGet("/past", async (IEventService service, int page = 1, int pageSize = 20) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);
            var result = await service.GetPastEventsAsync(page, pageSize);
            return Results.Ok(result);
        })
        .WithName("GetPastEvents")
        .Produces<PagedResult<EventResponse>>(StatusCodes.Status200OK);

        // 9. GET /by-category/{category} - GetEventsByCategory
        group.MapGet("/by-category/{category}", async (IEventService service, string category, int page = 1, int pageSize = 20) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);
            var result = await service.GetEventsByCategoryAsync(category, page, pageSize);
            return Results.Ok(result);
        })
        .WithName("GetEventsByCategory")
        .Produces<PagedResult<EventResponse>>(StatusCodes.Status200OK);

        // 10. GET /by-organizer/{organizerId:guid} - GetEventsByOrganizer
        group.MapGet("/by-organizer/{organizerId:guid}", async (IEventService service, Guid organizerId, int page = 1, int pageSize = 20) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);
            var result = await service.GetEventsByOrganizerAsync(organizerId, page, pageSize);
            return Results.Ok(result);
        })
        .WithName("GetEventsByOrganizer")
        .Produces<PagedResult<EventResponse>>(StatusCodes.Status200OK);

        // 11. POST /{id:guid}/register - RegisterForEvent
        group.MapPost("/{id:guid}/register", async (IEventService service, Guid id, RegisterRequest request) =>
        {
            var result = await service.RegisterForEventAsync(id, request);
            return result is not null
                ? Results.Created($"/api/v2/events/{id}/attendees", result)
                : Results.Conflict("Event is full or user already registered");
        })
        .RequireAuthorization()
        .WithName("RegisterForEvent")
        .Produces<AttendeeResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status409Conflict);

        // 12. DELETE /{id:guid}/register/{userId:guid} - UnregisterFromEvent
        group.MapDelete("/{id:guid}/register/{userId:guid}", async (IEventService service, Guid id, Guid userId, HttpContext httpContext) =>
        {
            var callerSub = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = httpContext.User.IsInRole("Admin");
            if (!isAdmin && callerSub != userId.ToString())
                return Results.Forbid();

            var removed = await service.UnregisterFromEventAsync(id, userId);
            return removed ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization()
        .WithName("UnregisterFromEvent")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 13. GET /{id:guid}/attendees - GetEventAttendees
        group.MapGet("/{id:guid}/attendees", async (IEventService service, Guid id) =>
        {
            var result = await service.GetEventAttendeesAsync(id);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetEventAttendees")
        .Produces<List<AttendeeResponse>>(StatusCodes.Status200OK);

        // 14. PUT /{id:guid}/publish - PublishEvent
        group.MapPut("/{id:guid}/publish", async (IEventService service, Guid id) =>
        {
            var result = await service.PublishEventAsync(id);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization("OrganizerOrAdmin")
        .WithName("PublishEvent")
        .Produces<EventResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 15. PUT /{id:guid}/cancel - CancelEvent
        group.MapPut("/{id:guid}/cancel", async (IEventService service, Guid id) =>
        {
            var result = await service.CancelEventAsync(id);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization("OrganizerOrAdmin")
        .WithName("CancelEvent")
        .Produces<EventResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 16. GET /{id:guid}/stats - GetEventStats
        group.MapGet("/{id:guid}/stats", async (IEventService service, Guid id) =>
        {
            var result = await service.GetEventStatsAsync(id);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization()
        .WithName("GetEventStats")
        .Produces<EventStatsResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 17. POST /{id:guid}/duplicate - DuplicateEvent
        group.MapPost("/{id:guid}/duplicate", async (IEventService service, Guid id) =>
        {
            var result = await service.DuplicateEventAsync(id);
            return result is not null
                ? Results.Created($"/api/v2/events/{result.Id}", result)
                : Results.NotFound();
        })
        .RequireAuthorization()
        .WithName("DuplicateEvent")
        .Produces<EventResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound);

        // 18. PATCH /{id:guid}/reschedule - RescheduleEvent
        group.MapPatch("/{id:guid}/reschedule", async (IEventService service, Guid id, RescheduleRequest request) =>
        {
            var result = await service.RescheduleEventAsync(id, request);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization("OrganizerOrAdmin")
        .WithName("RescheduleEvent")
        .Produces<EventResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
