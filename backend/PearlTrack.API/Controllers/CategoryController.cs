using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PearlTrack.API.DTOs;
using PearlTrack.API.Services;

namespace PearlTrack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly ILogger<CategoryController> _logger;

    public CategoryController(ICategoryService categoryService, ILogger<CategoryController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryCreateRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new category");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _categoryService.CreateAsync(request);

            return CreatedAtAction(nameof(GetCategoryById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid category creation request");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category");
            return StatusCode(500, "An error occurred while creating the category");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategoryById(string id)
    {
        try
        {
            var category = await _categoryService.GetByIdAsync(id);
            if (category == null)
                return NotFound();

            return Ok(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving category: {CategoryId}", id);
            return StatusCode(500, "An error occurred while retrieving the category");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCategories()
    {
        try
        {
            var categories = await _categoryService.GetAllAsync();

            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, "An error occurred while retrieving categories");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(string id, [FromBody] CategoryUpdateRequest request)
    {
        try
        {
            var result = await _categoryService.UpdateAsync(id, request);

            if (result == null)
                return NotFound();

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid category update request for category: {CategoryId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category: {CategoryId}", id);
            return StatusCode(500, "An error occurred while updating the category");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(string id)
    {
        try
        {
            var result = await _categoryService.DeleteAsync(id);

            if (!result)
                return NotFound();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category: {CategoryId}", id);
            return StatusCode(500, "An error occurred while deleting the category");
        }
    }
}
