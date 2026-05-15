namespace PearlTrack.API.Models;

public class TaskAssignee
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string TaskItemId { get; set; } = string.Empty;
    public virtual TaskItem? TaskItem { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser? User { get; set; }
    
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
