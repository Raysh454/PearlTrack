using Microsoft.EntityFrameworkCore;
using PearlTrack.API.Data;
using PearlTrack.API.DTOs;
using PearlTrack.API.Models;

namespace PearlTrack.API.Services;

public interface ITaskService
{
    Task<TaskResponse> CreateAsync(string userId, TaskCreateRequest request);
    Task<TaskResponse?> GetByIdAsync(string taskId);
    Task<TaskListResponse> GetUserTasksAsync(string userId, int pageNumber = 1, int pageSize = 10);
    Task<TaskListResponse> GetAllTasksAsync(int pageNumber = 1, int pageSize = 10, TaskStatusType? status = null, TaskPriority? priority = null);
    Task<TaskResponse?> UpdateAsync(string taskId, string userId, TaskUpdateRequest request);
    Task<bool> DeleteAsync(string taskId, string userId);
    Task<bool> AssignUserAsync(string taskId, string userId, string assigneeId);
    Task<bool> UnassignUserAsync(string taskId, string userId, string assigneeId);
}

public class TaskService : ITaskService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TaskService> _logger;

    public TaskService(AppDbContext dbContext, ILogger<TaskService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TaskResponse> CreateAsync(string userId, TaskCreateRequest request)
    {
        try
        {
            ValidateCreateRequest(request);

            var taskItem = new TaskItem
            {
                Title = request.Title,
                Description = request.Description,
                Priority = request.Priority,
                DueDate = request.DueDate,
                CreatedById = userId,
                Status = TaskStatusType.Pending
            };

            _dbContext.TaskItems.Add(taskItem);
            await _dbContext.SaveChangesAsync();

            // Add categories
            if (request.CategoryIds.Any())
            {
                var categories = await _dbContext.Categories
                    .Where(c => request.CategoryIds.Contains(c.Id))
                    .ToListAsync();

                foreach (var category in categories)
                {
                    _dbContext.TaskCategories.Add(new TaskCategory
                    {
                        TaskItemId = taskItem.Id,
                        CategoryId = category.Id
                    });
                }

                await _dbContext.SaveChangesAsync();
            }

            // Add assignees
            if (request.AssigneeIds.Any())
            {
                foreach (var assigneeId in request.AssigneeIds)
                {
                    var user = await _dbContext.Users.FindAsync(assigneeId);
                    if (user != null)
                    {
                        _dbContext.TaskAssignees.Add(new TaskAssignee
                        {
                            TaskItemId = taskItem.Id,
                            UserId = assigneeId
                        });
                    }
                }

                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("TaskItem created: {TaskItemId}", taskItem.Id);

            return await GetByIdAsync(taskItem.Id) ?? throw new InvalidOperationException("Failed to retrieve created task");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            throw;
        }
    }

    public async Task<TaskResponse?> GetByIdAsync(string taskId)
    {
        var taskItem = await _dbContext.TaskItems
            .Include(t => t.CreatedBy)
            .Include(t => t.Assignees)
            .ThenInclude(ta => ta.User)
            .Include(t => t.TaskCategories)
            .ThenInclude(tc => tc.Category)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        return taskItem != null ? MapToResponse(taskItem) : null;
    }

    public async Task<TaskListResponse> GetUserTasksAsync(string userId, int pageNumber = 1, int pageSize = 10)
    {
        var query = _dbContext.TaskItems
            .Where(t => t.Assignees.Any(ta => ta.UserId == userId) || t.CreatedById == userId)
            .Include(t => t.CreatedBy)
            .Include(t => t.Assignees)
            .ThenInclude(ta => ta.User)
            .Include(t => t.TaskCategories)
            .ThenInclude(tc => tc.Category)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();
        var taskItems = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new TaskListResponse
        {
            Items = taskItems.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<TaskListResponse> GetAllTasksAsync(int pageNumber = 1, int pageSize = 10, TaskStatusType? status = null, TaskPriority? priority = null)
    {
        var query = _dbContext.TaskItems.AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);

        var baseQuery = query
            .Include(t => t.CreatedBy)
            .Include(t => t.Assignees)
            .ThenInclude(ta => ta.User)
            .Include(t => t.TaskCategories)
            .ThenInclude(tc => tc.Category)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await baseQuery.CountAsync();
        var taskItems = await baseQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new TaskListResponse
        {
            Items = taskItems.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<TaskResponse?> UpdateAsync(string taskId, string userId, TaskUpdateRequest request)
    {
        try
        {
            var taskItem = await _dbContext.TaskItems
                .Include(t => t.CreatedBy)
                .Include(t => t.Assignees)
                .ThenInclude(ta => ta.User)
                .Include(t => t.TaskCategories)
                .ThenInclude(tc => tc.Category)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (taskItem == null)
                return null;

            if (!IsUserAuthorized(taskItem, userId))
                throw new UnauthorizedAccessException("Only the task creator or admin can update this task");

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                if (request.Title.Length > 255)
                    throw new ArgumentException("Title must be 255 characters or less");
                taskItem.Title = request.Title;
            }

            if (request.Description != null)
            {
                if (request.Description.Length > 5000)
                    throw new ArgumentException("Description must be 5000 characters or less");
                taskItem.Description = request.Description;
            }

            if (request.Priority.HasValue)
                taskItem.Priority = request.Priority.Value;

            if (request.Status.HasValue)
                taskItem.Status = request.Status.Value;

            if (request.DueDate.HasValue)
                taskItem.DueDate = request.DueDate.Value;

            if (request.CategoryIds != null)
            {
                var existingCategories = _dbContext.TaskCategories.Where(tc => tc.TaskItemId == taskId);
                _dbContext.TaskCategories.RemoveRange(existingCategories);

                var newCategories = await _dbContext.Categories
                    .Where(c => request.CategoryIds.Contains(c.Id))
                    .ToListAsync();

                foreach (var category in newCategories)
                {
                    _dbContext.TaskCategories.Add(new TaskCategory
                    {
                        TaskItemId = taskItem.Id,
                        CategoryId = category.Id
                    });
                }
            }

            taskItem.UpdatedAt = DateTime.UtcNow;

            _dbContext.TaskItems.Update(taskItem);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("TaskItem updated: {TaskItemId}", taskItem.Id);

            return MapToResponse(taskItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string taskId, string userId)
    {
        try
        {
            var taskItem = await _dbContext.TaskItems.FindAsync(taskId);
            if (taskItem == null)
                return false;

            if (!IsUserAuthorized(taskItem, userId))
                throw new UnauthorizedAccessException("Only the task creator or admin can delete this task");

            _dbContext.TaskItems.Remove(taskItem);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("TaskItem deleted: {TaskItemId}", taskId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<bool> AssignUserAsync(string taskId, string userId, string assigneeId)
    {
        try
        {
            var taskItem = await _dbContext.TaskItems.FindAsync(taskId);
            if (taskItem == null)
                return false;

            if (!IsUserAuthorized(taskItem, userId))
                throw new UnauthorizedAccessException("Only the task creator or admin can assign users");

            var user = await _dbContext.Users.FindAsync(assigneeId);
            if (user == null)
                return false;

            var existingAssignment = await _dbContext.TaskAssignees
                .FirstOrDefaultAsync(ta => ta.TaskItemId == taskId && ta.UserId == assigneeId);

            if (existingAssignment != null)
                return true;

            _dbContext.TaskAssignees.Add(new TaskAssignee
            {
                TaskItemId = taskId,
                UserId = assigneeId
            });

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} assigned to task {TaskItemId}", assigneeId, taskId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning user to task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<bool> UnassignUserAsync(string taskId, string userId, string assigneeId)
    {
        try
        {
            var taskItem = await _dbContext.TaskItems.FindAsync(taskId);
            if (taskItem == null)
                return false;

            if (!IsUserAuthorized(taskItem, userId))
                throw new UnauthorizedAccessException("Only the task creator or admin can unassign users");

            var assignment = await _dbContext.TaskAssignees
                .FirstOrDefaultAsync(ta => ta.TaskItemId == taskId && ta.UserId == assigneeId);

            if (assignment == null)
                return false;

            _dbContext.TaskAssignees.Remove(assignment);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} unassigned from task {TaskItemId}", assigneeId, taskId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning user from task: {TaskId}", taskId);
            throw;
        }
    }

    private void ValidateCreateRequest(TaskCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Task title is required");

        if (request.Title.Length > 255)
            throw new ArgumentException("Task title must be 255 characters or less");

        if (request.Description?.Length > 5000)
            throw new ArgumentException("Task description must be 5000 characters or less");

        if (request.DueDate.HasValue && request.DueDate < DateTime.UtcNow)
            throw new ArgumentException("Due date must be in the future");
    }

    private static bool IsUserAuthorized(TaskItem taskItem, string userId)
    {
        return taskItem.CreatedById == userId;
    }

    private static TaskResponse MapToResponse(TaskItem taskItem)
    {
        return new TaskResponse
        {
            Id = taskItem.Id,
            Title = taskItem.Title,
            Description = taskItem.Description,
            Priority = taskItem.Priority,
            Status = taskItem.Status,
            DueDate = taskItem.DueDate,
            CreatedById = taskItem.CreatedById,
            CreatedByName = $"{taskItem.CreatedBy?.FirstName} {taskItem.CreatedBy?.LastName}".Trim(),
            CreatedAt = taskItem.CreatedAt,
            UpdatedAt = taskItem.UpdatedAt,
            Assignees = taskItem.Assignees.Select(ta => new TaskAssigneeDto
            {
                UserId = ta.UserId,
                UserName = ta.User?.UserName ?? string.Empty,
                Email = ta.User?.Email ?? string.Empty,
                AssignedAt = ta.AssignedAt
            }).ToList(),
            Categories = taskItem.TaskCategories.Select(tc => new CategoryResponse
            {
                Id = tc.Category?.Id ?? string.Empty,
                Name = tc.Category?.Name ?? string.Empty,
                Description = tc.Category?.Description,
                CreatedAt = tc.Category?.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = tc.Category?.UpdatedAt
            }).ToList()
        };
    }
}
