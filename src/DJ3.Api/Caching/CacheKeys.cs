namespace DJ3.Api.Caching;

public static class CacheKeys
{
    public static string TenantPrefix(Guid tenantId) => $"t:{tenantId}:";
    public static string EventPrefix(Guid tenantId) => $"t:{tenantId}:event:";
    public static string EventsPrefix(Guid tenantId) => $"t:{tenantId}:events:";
    public static string AllEvents(Guid tenantId) => $"t:{tenantId}:events:all";
    public static string UpcomingEvents(Guid tenantId) => $"t:{tenantId}:events:upcoming";
    public static string PastEvents(Guid tenantId) => $"t:{tenantId}:events:past";

    public static string EventById(Guid tenantId, Guid id) => $"t:{tenantId}:event:{id}";
    public static string EventsByCategory(Guid tenantId, string category) => $"t:{tenantId}:events:category:{category}";
    public static string EventsByOrganizer(Guid tenantId, Guid organizerId) => $"t:{tenantId}:events:organizer:{organizerId}";
    public static string EventStats(Guid tenantId, Guid id) => $"t:{tenantId}:event:{id}:stats";
    public static string EventAttendees(Guid tenantId, Guid id) => $"t:{tenantId}:event:{id}:attendees";
    public static string SearchResults(Guid tenantId, string query) => $"t:{tenantId}:events:search:{query}";
}
