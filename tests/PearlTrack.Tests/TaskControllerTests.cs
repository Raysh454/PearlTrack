using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PearlTrack.API.Controllers;
using PearlTrack.API.Data;
using PearlTrack.API.DTOs;
using PearlTrack.API.Models;
using PearlTrack.API.Services;
using System.Security.Claims;

namespace PearlTrack.Tests;

public class TaskControllerTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _dbContext;
    private readonly ITaskService _taskService;
    private readonly TaskController _controller;
    private string _testUserId = string.Empty;
    private string _unauthorizedUserId = string.Empty;
    private string _assigneeUserId = string.Empty;
    private string _testCategoryId = string.Empty;
    private readonly Mock<ILogger<TaskController>> _loggerMock;

    public TaskControllerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        _dbContext.Database.EnsureCreated();

        var mockLoggerService = new Mock<ILogger<TaskService>>();
        _taskService = new TaskService(_dbContext, mockLoggerService.Object);

        _loggerMock = new Mock<ILogger<TaskController>>();
        _controller = new TaskController(_taskService, _loggerMock.Object);

        SetupTestData();
        SetupControllerContext();
    }

    private void SetupTestData()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testuser",
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "User"
        };

        var unauthorizedUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "unauthorized",
            Email = "unauthorized@test.com",
            FirstName = "Unauthorized",
            LastName = "User"
        };

        var assigneeUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "assignee",
            Email = "assignee@test.com",
            FirstName = "Assignee",
            LastName = "User"
        };

        _testUserId = user.Id;
        _unauthorizedUserId = unauthorizedUser.Id;
        _assigneeUserId = assigneeUser.Id;

        _dbContext.Users.Add(user);
        _dbContext.Users.Add(unauthorizedUser);
        _dbContext.Users.Add(assigneeUser);

        var category = new Category { Name = "Work" };
        _testCategoryId = category.Id;
        _dbContext.Categories.Add(category);

        _dbContext.SaveChanges();
    }

    private void SetupControllerContext(string userId = "")
    {
        var httpContextMock = new Mock<HttpContext>();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, string.IsNullOrEmpty(userId) ? _testUserId : userId)
        }, "TestScheme"));

        httpContextMock.Setup(c => c.User).Returns(claimsPrincipal);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContextMock.Object
        };
    }

    #region CreateTask Tests

    [Fact]
    public async Task CreateTask_WithValidData_ReturnsCreatedAtActionWith201()
    {
        // Arrange
        var request = new TaskCreateRequest
        {
            Title = "Test Task",
            Description = "Test Description",
            Priority = TaskPriority.High,
            DueDate = DateTime.UtcNow.AddDays(5),
            CategoryIds = new List<string> { _testCategoryId },
            AssigneeIds = new List<string> { _assigneeUserId }
        };

        // Act
        var result = await _controller.CreateTask(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(TaskController.GetTaskById), createdResult.ActionName);
        Assert.Equal(201, createdResult.StatusCode);
        
        var taskResponse = Assert.IsType<TaskResponse>(createdResult.Value);
        Assert.Equal("Test Task", taskResponse.Title);
        Assert.Equal("Test Description", taskResponse.Description);
        Assert.Equal(TaskPriority.High, taskResponse.Priority);
    }

    [Fact]
    public async Task CreateTask_WithInvalidData_ReturnsBadRequest400()
    {
        // Arrange - Empty title
        var request = new TaskCreateRequest
        {
            Title = string.Empty,
            Description = "Test Description"
        };

        // Act
        var result = await _controller.CreateTask(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateTask_WithTitleTooLong_ReturnsBadRequest400()
    {
        // Arrange
        var request = new TaskCreateRequest
        {
            Title = new string('a', 256),
            Description = "Test Description"
        };

        // Act
        var result = await _controller.CreateTask(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateTask_WithPastDueDate_ReturnsBadRequest400()
    {
        // Arrange
        var request = new TaskCreateRequest
        {
            Title = "Test Task",
            Description = "Test Description",
            DueDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var result = await _controller.CreateTask(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region GetTaskById Tests

    [Fact]
    public async Task GetTaskById_WithValidId_ReturnsOkWith200()
    {
        // Arrange
        var createRequest = new TaskCreateRequest
        {
            Title = "Get Task Test",
            Description = "Test Description",
            Priority = TaskPriority.Medium
        };
        var createdTask = await _taskService.CreateAsync(_testUserId, createRequest);

        // Act
        var result = await _controller.GetTaskById(createdTask.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        
        var taskResponse = Assert.IsType<TaskResponse>(okResult.Value);
        Assert.Equal(createdTask.Id, taskResponse.Id);
        Assert.Equal("Get Task Test", taskResponse.Title);
    }

    [Fact]
    public async Task GetTaskById_WithNonExistentId_ReturnsNotFound404()
    {
        // Act
        var result = await _controller.GetTaskById("nonexistent-id");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    #endregion

    #region GetAllTasks Tests

    [Fact]
    public async Task GetAllTasks_WithDefaults_ReturnsOkWith200()
    {
        // Arrange
        var request = new TaskCreateRequest { Title = "All Tasks Test 1" };
        await _taskService.CreateAsync(_testUserId, request);

        // Act
        var result = await _controller.GetAllTasks();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        
        var listResponse = Assert.IsType<TaskListResponse>(okResult.Value);
        Assert.NotEmpty(listResponse.Items);
    }

    [Fact]
    public async Task GetAllTasks_WithStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        var request1 = new TaskCreateRequest { Title = "Pending Task" };
        var request2 = new TaskCreateRequest 
        { 
            Title = "Completed Task",
            Priority = TaskPriority.High
        };
        
        var task1 = await _taskService.CreateAsync(_testUserId, request1);
        var task2 = await _taskService.CreateAsync(_testUserId, request2);
        
        await _taskService.UpdateAsync(task2.Id, _testUserId, 
            new TaskUpdateRequest { Status = TaskStatusType.Completed });

        // Act
        var result = await _controller.GetAllTasks(status: TaskStatusType.Completed);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var listResponse = Assert.IsType<TaskListResponse>(okResult.Value);
        
        Assert.All(listResponse.Items, item => 
            Assert.Equal(TaskStatusType.Completed, item.Status)
        );
    }

    [Fact]
    public async Task GetAllTasks_WithPriorityFilter_ReturnsFilteredResults()
    {
        // Arrange
        var request1 = new TaskCreateRequest { Title = "Low Priority", Priority = TaskPriority.Low };
        var request2 = new TaskCreateRequest { Title = "High Priority", Priority = TaskPriority.High };
        
        await _taskService.CreateAsync(_testUserId, request1);
        await _taskService.CreateAsync(_testUserId, request2);

        // Act
        var result = await _controller.GetAllTasks(priority: TaskPriority.High);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var listResponse = Assert.IsType<TaskListResponse>(okResult.Value);
        
        Assert.All(listResponse.Items, item => 
            Assert.Equal(TaskPriority.High, item.Priority)
        );
    }

    [Fact]
    public async Task GetAllTasks_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            var request = new TaskCreateRequest { Title = $"Task {i}" };
            await _taskService.CreateAsync(_testUserId, request);
        }

        // Act
        var result = await _controller.GetAllTasks(pageNumber: 1, pageSize: 5);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var listResponse = Assert.IsType<TaskListResponse>(okResult.Value);
        
        Assert.Equal(5, listResponse.Items.Count());
        Assert.Equal(1, listResponse.PageNumber);
        Assert.Equal(5, listResponse.PageSize);
    }

    #endregion

    #region GetMyTasks Tests

    [Fact]
    public async Task GetMyTasks_ReturnsOnlyUserTasks()
    {
        // Arrange
        var request1 = new TaskCreateRequest { Title = "My Task 1" };
        var request2 = new TaskCreateRequest { Title = "My Task 2" };
        
        await _taskService.CreateAsync(_testUserId, request1);
        await _taskService.CreateAsync(_testUserId, request2);
        
        var otherRequest = new TaskCreateRequest { Title = "Other User Task" };
        await _taskService.CreateAsync(_unauthorizedUserId, otherRequest);

        // Act
        var result = await _controller.GetMyTasks();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var listResponse = Assert.IsType<TaskListResponse>(okResult.Value);
        
        Assert.NotEmpty(listResponse.Items);
        Assert.All(listResponse.Items, item =>
            Assert.True(item.CreatedById == _testUserId || 
                       item.Assignees.Any(a => a.UserId == _testUserId))
        );
    }

    [Fact]
    public async Task GetMyTasks_WithPagination_ReturnsPaginatedUserTasks()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            var request = new TaskCreateRequest { Title = $"My Task {i}" };
            await _taskService.CreateAsync(_testUserId, request);
        }

        // Act
        var result = await _controller.GetMyTasks(pageNumber: 2, pageSize: 5);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var listResponse = Assert.IsType<TaskListResponse>(okResult.Value);
        
        Assert.Equal(5, listResponse.Items.Count());
        Assert.Equal(2, listResponse.PageNumber);
    }

    #endregion

    #region UpdateTask Tests

    [Fact]
    public async Task UpdateTask_WithValidData_ReturnsOkWith200()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Original Title", Priority = TaskPriority.Low };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        var updateRequest = new TaskUpdateRequest
        {
            Title = "Updated Title",
            Priority = TaskPriority.High,
            Status = TaskStatusType.InProgress
        };

        // Act
        var result = await _controller.UpdateTask(created.Id, updateRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        
        var taskResponse = Assert.IsType<TaskResponse>(okResult.Value);
        Assert.Equal("Updated Title", taskResponse.Title);
        Assert.Equal(TaskPriority.High, taskResponse.Priority);
        Assert.Equal(TaskStatusType.InProgress, taskResponse.Status);
    }

    [Fact]
    public async Task UpdateTask_ByUnauthorizedUser_ReturnsForbidden403()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Test Task" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        SetupControllerContext(_unauthorizedUserId);

        var updateRequest = new TaskUpdateRequest { Title = "Updated Title" };

        // Act
        var result = await _controller.UpdateTask(created.Id, updateRequest);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateTask_WithNonExistentId_ReturnsNotFound404()
    {
        // Arrange
        var updateRequest = new TaskUpdateRequest { Title = "Updated Title" };

        // Act
        var result = await _controller.UpdateTask("nonexistent-id", updateRequest);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_WithInvalidData_ReturnsBadRequest400()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Test Task" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        var updateRequest = new TaskUpdateRequest
        {
            Title = new string('a', 256)
        };

        // Act
        var result = await _controller.UpdateTask(created.Id, updateRequest);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region DeleteTask Tests

    [Fact]
    public async Task DeleteTask_WithValidId_ReturnsNoContent204()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task to Delete" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        // Act
        var result = await _controller.DeleteTask(created.Id);

        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContentResult.StatusCode);

        // Verify task is deleted
        var deleted = await _taskService.GetByIdAsync(created.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteTask_ByUnauthorizedUser_ReturnsForbidden403()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Test Task" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        SetupControllerContext(_unauthorizedUserId);

        // Act
        var result = await _controller.DeleteTask(created.Id);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DeleteTask_WithNonExistentId_ReturnsNotFound404()
    {
        // Act
        var result = await _controller.DeleteTask("nonexistent-id");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    #endregion

    #region AssignUser Tests

    [Fact]
    public async Task AssignUser_WithValidData_ReturnsOkWith200()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task to Assign" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        // Act
        var result = await _controller.AssignUser(created.Id, _assigneeUserId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        // Verify user is assigned
        var task = await _taskService.GetByIdAsync(created.Id);
        Assert.NotNull(task);
        Assert.Single(task!.Assignees);
        Assert.Equal(_assigneeUserId, task.Assignees.First().UserId);
    }

    [Fact]
    public async Task AssignUser_ByUnauthorizedUser_ReturnsForbidden403()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task to Assign" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        SetupControllerContext(_unauthorizedUserId);

        // Act
        var result = await _controller.AssignUser(created.Id, _assigneeUserId);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task AssignUser_ToNonExistentTask_ReturnsNotFound404()
    {
        // Act
        var result = await _controller.AssignUser("nonexistent-id", _assigneeUserId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task AssignUser_WithNonExistentAssignee_ReturnsNotFound404()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task to Assign" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        // Act
        var result = await _controller.AssignUser(created.Id, "nonexistent-user-id");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task AssignUser_MultipleUsers_AssignsAll()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Multi-assign Task" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        // Act
        var result1 = await _controller.AssignUser(created.Id, _assigneeUserId);
        
        var anotherUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "another",
            Email = "another@test.com"
        };
        _dbContext.Users.Add(anotherUser);
        _dbContext.SaveChanges();

        var result2 = await _controller.AssignUser(created.Id, anotherUser.Id);

        // Assert
        Assert.IsType<OkObjectResult>(result1);
        Assert.IsType<OkObjectResult>(result2);

        var task = await _taskService.GetByIdAsync(created.Id);
        Assert.NotNull(task);
        Assert.Equal(2, task!.Assignees.Count);
    }

    #endregion

    #region UnassignUser Tests

    [Fact]
    public async Task UnassignUser_WithValidData_ReturnsNoContent204()
    {
        // Arrange
        var createRequest = new TaskCreateRequest 
        { 
            Title = "Task to Unassign",
            AssigneeIds = new List<string> { _assigneeUserId }
        };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        // Act
        var result = await _controller.UnassignUser(created.Id, _assigneeUserId);

        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContentResult.StatusCode);

        // Verify user is unassigned
        var task = await _taskService.GetByIdAsync(created.Id);
        Assert.NotNull(task);
        Assert.Empty(task!.Assignees);
    }

    [Fact]
    public async Task UnassignUser_ByUnauthorizedUser_ReturnsForbidden403()
    {
        // Arrange
        var createRequest = new TaskCreateRequest 
        { 
            Title = "Task to Unassign",
            AssigneeIds = new List<string> { _assigneeUserId }
        };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        SetupControllerContext(_unauthorizedUserId);

        // Act
        var result = await _controller.UnassignUser(created.Id, _assigneeUserId);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UnassignUser_FromNonExistentTask_ReturnsNotFound404()
    {
        // Act
        var result = await _controller.UnassignUser("nonexistent-id", _assigneeUserId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task UnassignUser_NotAssigned_ReturnsNotFound404()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        // Act
        var result = await _controller.UnassignUser(created.Id, _assigneeUserId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetMyTasks_WithDifferentUser_ReturnsOnlyTheirTasks()
    {
        // Arrange
        var user1Tasks = new[] 
        {
            new TaskCreateRequest { Title = "User1 Task 1" },
            new TaskCreateRequest { Title = "User1 Task 2" }
        };
        
        var user2Tasks = new[] 
        {
            new TaskCreateRequest { Title = "User2 Task 1" }
        };

        foreach (var task in user1Tasks)
            await _taskService.CreateAsync(_testUserId, task);

        foreach (var task in user2Tasks)
            await _taskService.CreateAsync(_unauthorizedUserId, task);

        SetupControllerContext(_testUserId);

        // Act
        var result = await _controller.GetMyTasks();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var listResponse = Assert.IsType<TaskListResponse>(okResult.Value);
        
        Assert.Equal(2, listResponse.Items.Count());
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task CreateTask_WithNullRequest_ReturnsBadRequest400()
    {
        // Act & Assert
        var result = await _controller.CreateTask(new TaskCreateRequest());
        
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAllTasks_ReturnsSuccessEvenWithoutTasks()
    {
        // Act
        var result = await _controller.GetAllTasks();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        
        var listResponse = Assert.IsType<TaskListResponse>(okResult.Value);
        Assert.Empty(listResponse.Items);
    }

    #endregion

    #region Status Code Verification Tests

    [Fact]
    public async Task VerifyAllStatusCodes_CreateTask201()
    {
        // Verify CreateTask returns 201 Created
        var request = new TaskCreateRequest { Title = "Status Code Test" };
        var result = await _controller.CreateTask(request);
        
        Assert.IsType<CreatedAtActionResult>(result);
        var createdResult = (CreatedAtActionResult)result;
        Assert.Equal(201, createdResult.StatusCode);
    }

    [Fact]
    public async Task VerifyAllStatusCodes_GetTaskById200()
    {
        // Verify GetTaskById returns 200 OK
        var request = new TaskCreateRequest { Title = "Status Code Test" };
        var created = await _taskService.CreateAsync(_testUserId, request);
        
        var result = await _controller.GetTaskById(created.Id);
        
        Assert.IsType<OkObjectResult>(result);
        var okResult = (OkObjectResult)result;
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task VerifyAllStatusCodes_UpdateTask200()
    {
        // Verify UpdateTask returns 200 OK
        var request = new TaskCreateRequest { Title = "Status Code Test" };
        var created = await _taskService.CreateAsync(_testUserId, request);
        
        var updateRequest = new TaskUpdateRequest { Title = "Updated" };
        var result = await _controller.UpdateTask(created.Id, updateRequest);
        
        Assert.IsType<OkObjectResult>(result);
        var okResult = (OkObjectResult)result;
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task VerifyAllStatusCodes_DeleteTask204()
    {
        // Verify DeleteTask returns 204 NoContent
        var request = new TaskCreateRequest { Title = "Status Code Test" };
        var created = await _taskService.CreateAsync(_testUserId, request);
        
        var result = await _controller.DeleteTask(created.Id);
        
        Assert.IsType<NoContentResult>(result);
        var noContentResult = (NoContentResult)result;
        Assert.Equal(204, noContentResult.StatusCode);
    }

    [Fact]
    public async Task VerifyAllStatusCodes_UnassignUser204()
    {
        // Verify UnassignUser returns 204 NoContent
        var request = new TaskCreateRequest 
        { 
            Title = "Status Code Test",
            AssigneeIds = new List<string> { _assigneeUserId }
        };
        var created = await _taskService.CreateAsync(_testUserId, request);
        
        var result = await _controller.UnassignUser(created.Id, _assigneeUserId);
        
        Assert.IsType<NoContentResult>(result);
        var noContentResult = (NoContentResult)result;
        Assert.Equal(204, noContentResult.StatusCode);
    }

    [Fact]
    public async Task VerifyAllStatusCodes_NotFound404()
    {
        // Verify not found returns 404
        var result = await _controller.GetTaskById("nonexistent");
        
        Assert.IsType<NotFoundResult>(result);
        var notFoundResult = (NotFoundResult)result;
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task VerifyAllStatusCodes_Forbidden403()
    {
        // Verify unauthorized update returns 403 Forbidden
        var request = new TaskCreateRequest { Title = "Auth Test" };
        var created = await _taskService.CreateAsync(_testUserId, request);
        
        SetupControllerContext(_unauthorizedUserId);
        
        var updateRequest = new TaskUpdateRequest { Title = "Updated" };
        var result = await _controller.UpdateTask(created.Id, updateRequest);
        
        Assert.IsType<ForbidResult>(result);
    }

    #endregion
}
