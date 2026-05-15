namespace PearlTrack.API.DTOs;

public class TaskListResponse
{
    public List<TaskResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
