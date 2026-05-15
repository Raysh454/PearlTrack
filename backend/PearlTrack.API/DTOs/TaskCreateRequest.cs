using PearlTrack.API.Models;

namespace PearlTrack.API.DTOs;

public class TaskCreateRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }
    public List<string> CategoryIds { get; set; } = new();
    public List<string> AssigneeIds { get; set; } = new();
}
