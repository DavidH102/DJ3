namespace DJ3.Api.Models.DTOs;

public class AttendeeResponse
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}
