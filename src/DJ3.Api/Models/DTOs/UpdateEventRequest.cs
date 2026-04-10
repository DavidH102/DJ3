namespace DJ3.Api.Models.DTOs;

public class UpdateEventRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Category { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxCapacity { get; set; }
    public List<string>? Tags { get; set; }
}
