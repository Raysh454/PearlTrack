using Microsoft.EntityFrameworkCore;
using PearlTrack.API.Data;
using PearlTrack.API.DTOs;
using PearlTrack.API.Models;

namespace PearlTrack.API.Services;

public interface ICategoryService
{
    Task<CategoryResponse> CreateAsync(CategoryCreateRequest request);
    Task<CategoryResponse?> GetByIdAsync(string id);
    Task<List<CategoryResponse>> GetAllAsync();
    Task<CategoryResponse?> UpdateAsync(string id, CategoryUpdateRequest request);
    Task<bool> DeleteAsync(string id);
}

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(AppDbContext dbContext, ILogger<CategoryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CategoryResponse> CreateAsync(CategoryCreateRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Category name is required");

            if (request.Name.Length > 100)
                throw new ArgumentException("Category name must be 100 characters or less");

            var category = new Category
            {
                Name = request.Name,
                Description = request.Description
            };

            _dbContext.Categories.Add(category);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Category created: {CategoryId}", category.Id);

            return MapToResponse(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category");
            throw;
        }
    }

    public async Task<CategoryResponse?> GetByIdAsync(string id)
    {
        var category = await _dbContext.Categories.FindAsync(id);
        return category != null ? MapToResponse(category) : null;
    }

    public async Task<List<CategoryResponse>> GetAllAsync()
    {
        var categories = await _dbContext.Categories.OrderBy(c => c.Name).ToListAsync();
        return categories.Select(MapToResponse).ToList();
    }

    public async Task<CategoryResponse?> UpdateAsync(string id, CategoryUpdateRequest request)
    {
        try
        {
            var category = await _dbContext.Categories.FindAsync(id);
            if (category == null)
                return null;

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                if (request.Name.Length > 100)
                    throw new ArgumentException("Category name must be 100 characters or less");
                category.Name = request.Name;
            }

            if (request.Description != null)
                category.Description = request.Description;

            category.UpdatedAt = DateTime.UtcNow;

            _dbContext.Categories.Update(category);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Category updated: {CategoryId}", category.Id);

            return MapToResponse(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category: {CategoryId}", id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        try
        {
            var category = await _dbContext.Categories.FindAsync(id);
            if (category == null)
                return false;

            _dbContext.Categories.Remove(category);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Category deleted: {CategoryId}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category: {CategoryId}", id);
            throw;
        }
    }

    private static CategoryResponse MapToResponse(Category category)
    {
        return new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }
}
