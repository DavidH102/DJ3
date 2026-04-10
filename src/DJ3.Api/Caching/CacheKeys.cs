namespace DJ3.Api.Caching;

public static class CacheKeys
{
    public const string EventPrefix = "event:";
    public const string EventsPrefix = "events:";
    public const string AllEvents = "events:all";
    public const string UpcomingEvents = "events:upcoming";
    public const string PastEvents = "events:past";

    public static string EventById(Guid id) => $"event:{id}";
    public static string EventsByCategory(string category) => $"events:category:{category}";
    public static string EventsByOrganizer(Guid organizerId) => $"events:organizer:{organizerId}";
    public static string EventStats(Guid id) => $"event:{id}:stats";
    public static string EventAttendees(Guid id) => $"event:{id}:attendees";
    public static string SearchResults(string query) => $"events:search:{query}";
}
