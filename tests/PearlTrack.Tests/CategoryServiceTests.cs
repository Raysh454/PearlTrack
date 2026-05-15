using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PearlTrack.API.Data;
using PearlTrack.API.DTOs;
using PearlTrack.API.Services;

namespace PearlTrack.Tests;

public class CategoryServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _dbContext;
    private readonly ICategoryService _categoryService;
    private readonly ILogger<CategoryService> _loggerMock;

    public CategoryServiceTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        _dbContext.Database.EnsureCreated();

        var mockLogger = new Mock<ILogger<CategoryService>>();
        _loggerMock = mockLogger.Object;
        _categoryService = new CategoryService(_dbContext, _loggerMock);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsCategoryResponse()
    {
        // Arrange
        var request = new CategoryCreateRequest
        {
            Name = "Work",
            Description = "Work-related tasks"
        };

        // Act
        var result = await _categoryService.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.Description, result.Description);
        Assert.NotEmpty(result.Id);
    }

    [Fact]
    public async Task CreateAsync_WithoutName_ThrowsArgumentException()
    {
        // Arrange
        var request = new CategoryCreateRequest { Name = string.Empty };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _categoryService.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_WithNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var request = new CategoryCreateRequest
        {
            Name = new string('a', 101)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _categoryService.CreateAsync(request));
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsCategoryResponse()
    {
        // Arrange
        var createRequest = new CategoryCreateRequest { Name = "Personal" };
        var created = await _categoryService.CreateAsync(createRequest);

        // Act
        var result = await _categoryService.GetByIdAsync(created.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.Name, result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _categoryService.GetByIdAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllCategories()
    {
        // Arrange
        var request1 = new CategoryCreateRequest { Name = "Work" };
        var request2 = new CategoryCreateRequest { Name = "Personal" };
        var request3 = new CategoryCreateRequest { Name = "Shopping" };

        await _categoryService.CreateAsync(request1);
        await _categoryService.CreateAsync(request2);
        await _categoryService.CreateAsync(request3);

        // Act
        var result = await _categoryService.GetAllAsync();

        // Assert
        Assert.NotEmpty(result);
        Assert.True(result.Count >= 3);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCategoriesSortedByName()
    {
        // Arrange
        await _categoryService.CreateAsync(new CategoryCreateRequest { Name = "Zebra" });
        await _categoryService.CreateAsync(new CategoryCreateRequest { Name = "Apple" });
        await _categoryService.CreateAsync(new CategoryCreateRequest { Name = "Banana" });

        // Act
        var result = await _categoryService.GetAllAsync();

        // Assert
        var categories = result.Where(c => c.Name != null && (c.Name == "Apple" || c.Name == "Banana" || c.Name == "Zebra")).ToList();
        Assert.True(categories.Count >= 3);
        Assert.True(categories[0].Name!.CompareTo(categories[1].Name!) <= 0);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesCategory()
    {
        // Arrange
        var createRequest = new CategoryCreateRequest
        {
            Name = "Original Name",
            Description = "Original Description"
        };
        var created = await _categoryService.CreateAsync(createRequest);

        var updateRequest = new CategoryUpdateRequest
        {
            Name = "Updated Name",
            Description = "Updated Description"
        };

        // Act
        var result = await _categoryService.UpdateAsync(created.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("Updated Description", result.Description);
    }

    [Fact]
    public async Task UpdateAsync_WithPartialUpdate_UpdatesOnlyProvidedFields()
    {
        // Arrange
        var createRequest = new CategoryCreateRequest
        {
            Name = "Original Name",
            Description = "Original Description"
        };
        var created = await _categoryService.CreateAsync(createRequest);

        var updateRequest = new CategoryUpdateRequest { Name = "New Name" };

        // Act
        var result = await _categoryService.UpdateAsync(created.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Name", result.Name);
        Assert.Equal("Original Description", result.Description);
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var updateRequest = new CategoryUpdateRequest { Name = "Updated Name" };

        // Act
        var result = await _categoryService.UpdateAsync("nonexistent", updateRequest);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_WithNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var createRequest = new CategoryCreateRequest { Name = "Work" };
        var created = await _categoryService.CreateAsync(createRequest);

        var updateRequest = new CategoryUpdateRequest { Name = new string('a', 101) };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _categoryService.UpdateAsync(created.Id, updateRequest)
        );
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_DeletesCategory()
    {
        // Arrange
        var createRequest = new CategoryCreateRequest { Name = "Category to Delete" };
        var created = await _categoryService.CreateAsync(createRequest);

        // Act
        var result = await _categoryService.DeleteAsync(created.Id);

        // Assert
        Assert.True(result);
        var deleted = await _categoryService.GetByIdAsync(created.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ReturnsFalse()
    {
        // Act
        var result = await _categoryService.DeleteAsync("nonexistent");

        // Assert
        Assert.False(result);
    }
}
