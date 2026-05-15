using PearlTrack.API.Models;

namespace PearlTrack.API.DTOs;

public class TaskUpdateRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public TaskPriority? Priority { get; set; }
    public TaskStatusType? Status { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string>? CategoryIds { get; set; }
}
