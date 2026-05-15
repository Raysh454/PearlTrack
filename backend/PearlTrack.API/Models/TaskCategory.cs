namespace PearlTrack.API.Models;

public class TaskCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string TaskItemId { get; set; } = string.Empty;
    public virtual TaskItem? TaskItem { get; set; }
    
    public string CategoryId { get; set; } = string.Empty;
    public virtual Category? Category { get; set; }
    
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
