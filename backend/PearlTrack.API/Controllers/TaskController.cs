using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PearlTrack.API.DTOs;
using PearlTrack.API.Models;
using PearlTrack.API.Services;
using System.Security.Claims;

namespace PearlTrack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TaskController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ILogger<TaskController> _logger;

    public TaskController(ITaskService taskService, ILogger<TaskController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User ID not found");
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] TaskCreateRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new task");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _taskService.CreateAsync(userId, request);

            return CreatedAtAction(nameof(GetTaskById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid task creation request");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return StatusCode(500, "An error occurred while creating the task");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTaskById(string id)
    {
        try
        {
            var task = await _taskService.GetByIdAsync(id);
            if (task == null)
                return NotFound();

            return Ok(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task: {TaskId}", id);
            return StatusCode(500, "An error occurred while retrieving the task");
        }
    }

    [HttpGet("user/me")]
    public async Task<IActionResult> GetMyTasks([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetUserId();
            var result = await _taskService.GetUserTasksAsync(userId, pageNumber, pageSize);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user tasks");
            return StatusCode(500, "An error occurred while retrieving your tasks");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTasks(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] TaskStatusType? status = null,
        [FromQuery] TaskPriority? priority = null)
    {
        try
        {
            var result = await _taskService.GetAllTasksAsync(pageNumber, pageSize, status, priority);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all tasks");
            return StatusCode(500, "An error occurred while retrieving tasks");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(string id, [FromBody] TaskUpdateRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _taskService.UpdateAsync(id, userId, request);

            if (result == null)
                return NotFound();

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized task update attempt for task: {TaskId}", id);
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid task update request for task: {TaskId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task: {TaskId}", id);
            return StatusCode(500, "An error occurred while updating the task");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(string id)
    {
        try
        {
            var userId = GetUserId();
            var result = await _taskService.DeleteAsync(id, userId);

            if (!result)
                return NotFound();

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized task deletion attempt for task: {TaskId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task: {TaskId}", id);
            return StatusCode(500, "An error occurred while deleting the task");
        }
    }

    [HttpPost("{id}/assign/{assigneeId}")]
    public async Task<IActionResult> AssignUser(string id, string assigneeId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _taskService.AssignUserAsync(id, userId, assigneeId);

            if (!result)
                return NotFound();

            return Ok(new { message = "User assigned successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized assignment attempt for task: {TaskId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning user to task: {TaskId}", id);
            return StatusCode(500, "An error occurred while assigning the user");
        }
    }

    [HttpDelete("{id}/assign/{assigneeId}")]
    public async Task<IActionResult> UnassignUser(string id, string assigneeId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _taskService.UnassignUserAsync(id, userId, assigneeId);

            if (!result)
                return NotFound();

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized unassignment attempt for task: {TaskId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning user from task: {TaskId}", id);
            return StatusCode(500, "An error occurred while unassigning the user");
        }
    }
}
