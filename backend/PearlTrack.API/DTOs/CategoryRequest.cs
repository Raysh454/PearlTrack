namespace PearlTrack.API.DTOs;

public class CategoryResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CategoryCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CategoryUpdateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}
