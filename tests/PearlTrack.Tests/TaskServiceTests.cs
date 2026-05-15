using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PearlTrack.API.Data;
using PearlTrack.API.DTOs;
using PearlTrack.API.Models;
using PearlTrack.API.Services;

namespace PearlTrack.Tests;

public class TaskServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _dbContext;
    private readonly ITaskService _taskService;
    private readonly ILogger<TaskService> _loggerMock;
    private string _testUserId = string.Empty;
    private string _testAssigneeId = string.Empty;
    private string _testCategoryId = string.Empty;

    public TaskServiceTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        _dbContext.Database.EnsureCreated();

        var mockLogger = new Mock<ILogger<TaskService>>();
        _loggerMock = mockLogger.Object;
        _taskService = new TaskService(_dbContext, _loggerMock);

        SetupTestData();
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

        var assignee = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "assignee",
            Email = "assignee@test.com",
            FirstName = "Assignee",
            LastName = "User"
        };

        _testUserId = user.Id;
        _testAssigneeId = assignee.Id;

        _dbContext.Users.Add(user);
        _dbContext.Users.Add(assignee);

        var category = new Category
        {
            Name = "Work"
        };
        _testCategoryId = category.Id;
        _dbContext.Categories.Add(category);

        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsTaskResponse()
    {
        // Arrange
        var request = new TaskCreateRequest
        {
            Title = "Test Task",
            Description = "Test Description",
            Priority = TaskPriority.High,
            DueDate = DateTime.UtcNow.AddDays(5),
            CategoryIds = new List<string> { _testCategoryId },
            AssigneeIds = new List<string> { _testAssigneeId }
        };

        // Act
        var result = await _taskService.CreateAsync(_testUserId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Title, result.Title);
        Assert.Equal(request.Description, result.Description);
        Assert.Equal(request.Priority, result.Priority);
        Assert.Equal(TaskStatusType.Pending, result.Status);
        Assert.Single(result.Assignees);
        Assert.Single(result.Categories);
        Assert.Equal(_testUserId, result.CreatedById);
    }

    [Fact]
    public async Task CreateAsync_WithoutTitle_ThrowsArgumentException()
    {
        // Arrange
        var request = new TaskCreateRequest
        {
            Title = string.Empty,
            Description = "Test Description"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _taskService.CreateAsync(_testUserId, request));
    }

    [Fact]
    public async Task CreateAsync_WithTitleTooLong_ThrowsArgumentException()
    {
        // Arrange
        var request = new TaskCreateRequest
        {
            Title = new string('a', 256),
            Description = "Test Description"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _taskService.CreateAsync(_testUserId, request));
    }

    [Fact]
    public async Task CreateAsync_WithDescriptionTooLong_ThrowsArgumentException()
    {
        // Arrange
        var request = new TaskCreateRequest
        {
            Title = "Test Task",
            Description = new string('a', 5001)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _taskService.CreateAsync(_testUserId, request));
    }

    [Fact]
    public async Task CreateAsync_WithPastDueDate_ThrowsArgumentException()
    {
        // Arrange
        var request = new TaskCreateRequest
        {
            Title = "Test Task",
            Description = "Test Description",
            DueDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _taskService.CreateAsync(_testUserId, request));
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsTaskResponse()
    {
        // Arrange
        var createRequest = new TaskCreateRequest
        {
            Title = "Test Task",
            Description = "Test Description",
            Priority = TaskPriority.Medium
        };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        // Act
        var result = await _taskService.GetByIdAsync(created.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.Title, result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _taskService.GetByIdAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserTasksAsync_ReturnsOnlyUserTasks()
    {
        // Arrange
        var anotherUserId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = anotherUserId,
            UserName = "another",
            Email = "another@test.com"
        };
        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();

        var request1 = new TaskCreateRequest { Title = "User Task", Description = "Created by user" };
        var request2 = new TaskCreateRequest { Title = "Another Task", Description = "Created by another" };

        var task1 = await _taskService.CreateAsync(_testUserId, request1);
        var task2 = await _taskService.CreateAsync(anotherUserId, request2);

        await _taskService.AssignUserAsync(task2.Id, anotherUserId, _testUserId);

        // Act
        var result = await _taskService.GetUserTasksAsync(_testUserId);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item =>
            Assert.True(item.CreatedById == _testUserId || item.Assignees.Any(a => a.UserId == _testUserId))
        );
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesTask()
    {
        // Arrange
        var createRequest = new TaskCreateRequest
        {
            Title = "Original Title",
            Priority = TaskPriority.Low
        };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        var updateRequest = new TaskUpdateRequest
        {
            Title = "Updated Title",
            Priority = TaskPriority.Critical,
            Status = TaskStatusType.InProgress
        };

        // Act
        var result = await _taskService.UpdateAsync(created.Id, _testUserId, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal(TaskPriority.Critical, result.Priority);
        Assert.Equal(TaskStatusType.InProgress, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ByNonCreator_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Test Task" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        var updateRequest = new TaskUpdateRequest { Title = "Updated Title" };
        var unauthorizedUserId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _taskService.UpdateAsync(created.Id, unauthorizedUserId, updateRequest)
        );
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var updateRequest = new TaskUpdateRequest { Title = "Updated Title" };

        // Act
        var result = await _taskService.UpdateAsync("nonexistent", _testUserId, updateRequest);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_DeletesTask()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task to Delete" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        // Act
        var result = await _taskService.DeleteAsync(created.Id, _testUserId);

        // Assert
        Assert.True(result);
        var deleted = await _taskService.GetByIdAsync(created.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_ByNonCreator_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Test Task" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);
        var unauthorizedUserId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _taskService.DeleteAsync(created.Id, unauthorizedUserId)
        );
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ReturnsFalse()
    {
        // Act
        var result = await _taskService.DeleteAsync("nonexistent", _testUserId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AssignUserAsync_WithValidData_AssignsUser()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task to Assign" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        // Act
        var result = await _taskService.AssignUserAsync(created.Id, _testUserId, _testAssigneeId);

        // Assert
        Assert.True(result);
        var task = await _taskService.GetByIdAsync(created.Id);
        Assert.NotNull(task);
        Assert.Single(task.Assignees);
        Assert.Equal(_testAssigneeId, task.Assignees[0].UserId);
    }

    [Fact]
    public async Task AssignUserAsync_ByNonCreator_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task to Assign" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);
        var unauthorizedUserId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _taskService.AssignUserAsync(created.Id, unauthorizedUserId, _testAssigneeId)
        );
    }

    [Fact]
    public async Task AssignUserAsync_WithDuplicateAssignment_ReturnsTrueWithoutDuplicate()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task to Assign" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);

        await _taskService.AssignUserAsync(created.Id, _testUserId, _testAssigneeId);

        // Act
        var result = await _taskService.AssignUserAsync(created.Id, _testUserId, _testAssigneeId);

        // Assert
        Assert.True(result);
        var task = await _taskService.GetByIdAsync(created.Id);
        Assert.Single(task!.Assignees);
    }

    [Fact]
    public async Task UnassignUserAsync_WithValidData_UnassignsUser()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task to Unassign" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);
        await _taskService.AssignUserAsync(created.Id, _testUserId, _testAssigneeId);

        // Act
        var result = await _taskService.UnassignUserAsync(created.Id, _testUserId, _testAssigneeId);

        // Assert
        Assert.True(result);
        var task = await _taskService.GetByIdAsync(created.Id);
        Assert.Empty(task!.Assignees);
    }

    [Fact]
    public async Task UnassignUserAsync_ByNonCreator_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var createRequest = new TaskCreateRequest { Title = "Task to Unassign" };
        var created = await _taskService.CreateAsync(_testUserId, createRequest);
        await _taskService.AssignUserAsync(created.Id, _testUserId, _testAssigneeId);
        var unauthorizedUserId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _taskService.UnassignUserAsync(created.Id, unauthorizedUserId, _testAssigneeId)
        );
    }

    [Fact]
    public async Task GetAllTasksAsync_WithStatusFilter_ReturnsFilteredTasks()
    {
        // Arrange
        var request1 = new TaskCreateRequest { Title = "Pending Task" };
        var request2 = new TaskCreateRequest { Title = "Completed Task" };

        var task1 = await _taskService.CreateAsync(_testUserId, request1);
        var task2 = await _taskService.CreateAsync(_testUserId, request2);

        var updateRequest = new TaskUpdateRequest { Status = TaskStatusType.Completed };
        await _taskService.UpdateAsync(task2.Id, _testUserId, updateRequest);

        // Act
        var result = await _taskService.GetAllTasksAsync(status: TaskStatusType.Completed);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.Equal(TaskStatusType.Completed, item.Status));
    }

    [Fact]
    public async Task GetAllTasksAsync_WithPriorityFilter_ReturnsFilteredTasks()
    {
        // Arrange
        var request1 = new TaskCreateRequest { Title = "Low Priority", Priority = TaskPriority.Low };
        var request2 = new TaskCreateRequest { Title = "High Priority", Priority = TaskPriority.High };

        await _taskService.CreateAsync(_testUserId, request1);
        await _taskService.CreateAsync(_testUserId, request2);

        // Act
        var result = await _taskService.GetAllTasksAsync(priority: TaskPriority.High);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.Equal(TaskPriority.High, item.Priority));
    }

    [Fact]
    public async Task GetAllTasksAsync_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange - Create 15 tasks
        for (int i = 0; i < 15; i++)
        {
            var request = new TaskCreateRequest { Title = $"Task {i}" };
            await _taskService.CreateAsync(_testUserId, request);
        }

        // Act
        var page1 = await _taskService.GetAllTasksAsync(pageNumber: 1, pageSize: 10);
        var page2 = await _taskService.GetAllTasksAsync(pageNumber: 2, pageSize: 10);

        // Assert
        Assert.Equal(10, page1.Items.Count);
        Assert.True(page1.TotalCount >= 15);
        Assert.True(page2.Items.Count > 0);
    }
}
