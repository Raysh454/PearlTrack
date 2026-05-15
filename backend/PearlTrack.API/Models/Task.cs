namespace PearlTrack.API.Models;

public enum TaskStatusType
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskStatusType Status { get; set; } = TaskStatusType.Pending;
    public DateTime? DueDate { get; set; }
    
    public string CreatedById { get; set; } = string.Empty;
    public virtual ApplicationUser? CreatedBy { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public virtual ICollection<TaskAssignee> Assignees { get; set; } = new List<TaskAssignee>();
    public virtual ICollection<TaskCategory> TaskCategories { get; set; } = new List<TaskCategory>();
}
