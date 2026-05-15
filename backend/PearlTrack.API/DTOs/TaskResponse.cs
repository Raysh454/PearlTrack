using PearlTrack.API.Models;

namespace PearlTrack.API.DTOs;

public class TaskResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; }
    public TaskStatusType Status { get; set; }
    public DateTime? DueDate { get; set; }
    
    public string CreatedById { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public List<TaskAssigneeDto> Assignees { get; set; } = new();
    public List<CategoryResponse> Categories { get; set; } = new();
}

public class TaskAssigneeDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}
