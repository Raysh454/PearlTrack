using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PearlTrack.API.Data;
using PearlTrack.API.DTOs;
using PearlTrack.API.Models;
using PearlTrack.API.Services;

namespace PearlTrack.Tests;

public class AuthServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IAuthService _authService;
    private readonly Mock<ILogger<AuthService>> _loggerMock;

    public AuthServiceTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>();

        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        _userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        _signInManager = _serviceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        var mockConfig = new Mock<IConfiguration>();
        mockConfig
            .Setup(x => x[It.Is<string>(s => s == "Jwt:SecretKey")])
            .Returns("test-super-secret-key-at-least-32-characters-long-for-testing!!!");
        mockConfig
            .Setup(x => x[It.Is<string>(s => s == "Jwt:Issuer")])
            .Returns("TestAPI");
        mockConfig
            .Setup(x => x[It.Is<string>(s => s == "Jwt:Audience")])
            .Returns("TestClient");
        mockConfig
            .Setup(x => x[It.Is<string>(s => s == "Jwt:ExpirationMinutes")])
            .Returns("60");

        var mockConfigLogger = new Mock<ILogger<JwtTokenService>>();
        _tokenService = new JwtTokenService(mockConfig.Object, mockConfigLogger.Object);

        _loggerMock = new Mock<ILogger<AuthService>>();
        _authService = new AuthService(_userManager, _signInManager, _roleManager, _tokenService, _loggerMock.Object);

        // Initialize DB
        _dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_ReturnsSuccessResponse()
    {
        // Arrange
        // Ensure User role exists for registration
        await _roleManager.CreateAsync(new IdentityRole("User"));
        
        var registerRequest = new RegisterRequest
        {
            Email = "newuser@test.com",
            Username = "newuser",
            Password = "Test@123",
            FirstName = "John",
            LastName = "Doe"
        };

        // Act
        var result = await _authService.RegisterAsync(registerRequest);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Registration successful", result.Message);
        
        // Verify user was created
        var user = await _userManager.FindByEmailAsync(registerRequest.Email);
        Assert.NotNull(user);
        Assert.Equal(registerRequest.Username, user!.UserName);
        Assert.Equal(registerRequest.FirstName, user.FirstName);
        Assert.Equal(registerRequest.LastName, user.LastName);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ReturnsFail()
    {
        // Arrange
        await _roleManager.CreateAsync(new IdentityRole("User"));

        var email = "test@test.com";
        var firstRequest = new RegisterRequest
        {
            Email = email,
            Username = "user1",
            Password = "Test@123"
        };

        // Create first user
        await _authService.RegisterAsync(firstRequest);

        var duplicateRequest = new RegisterRequest
        {
            Email = email,
            Username = "user2",
            Password = "Test@123"
        };

        // Act
        var result = await _authService.RegisterAsync(duplicateRequest);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("already registered", result.Message ?? "");
    }

    [Fact]
    public async Task RegisterAsync_WithWeakPassword_ReturnsFail()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = "weak@test.com",
            Username = "weakuser",
            Password = "weak" // Does not meet password requirements
        };

        // Act
        var result = await _authService.RegisterAsync(registerRequest);

        // Assert
        Assert.False(result.Success);
        // AuthService returns "Registration failed: {errors}" when password is weak
        Assert.Contains("Registration failed", result.Message ?? "");
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenAndUser()
    {
        // Arrange
        // Create User role first
        await _roleManager.CreateAsync(new IdentityRole("User"));
        
        var email = "login@test.com";
        var password = "ValidPass@123";
        var user = new ApplicationUser
        {
            UserName = "loginuser",
            Email = email,
            FirstName = "Login",
            LastName = "User",
            EmailConfirmed = true
        };

        await _userManager.CreateAsync(user, password);
        await _userManager.AddToRoleAsync(user, "User");

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Token ?? "");
        Assert.NotNull(result.User);
        Assert.Equal(email, result.User?.Email);
        Assert.Contains("User", result.User?.Roles ?? new List<string>());
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsFail()
    {
        // Arrange
        var email = "wrongpass@test.com";
        var user = new ApplicationUser
        {
            UserName = "wrongpass",
            Email = email,
            EmailConfirmed = true
        };

        await _userManager.CreateAsync(user, "CorrectPass@123");

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "WrongPass@123"
        };

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.False(result.Success);
        // AuthService returns "Invalid email or password" for wrong password
        Assert.Contains("Invalid", result.Message ?? "");
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ReturnsFail()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "nonexistent@test.com",
            Password = "Test@123"
        };

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid", result.Message ?? "");
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task RegisterAsync_NewUserGetsUserRole()
    {
        // Arrange
        await _roleManager.CreateAsync(new IdentityRole("User"));
        
        var registerRequest = new RegisterRequest
        {
            Email = "roletest@test.com",
            Username = "roleuser",
            Password = "Test@123"
        };

        // Act
        var result = await _authService.RegisterAsync(registerRequest);
        var user = await _userManager.FindByEmailAsync(registerRequest.Email);
        var roles = await _userManager.GetRolesAsync(user!);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("User", roles);
    }
}
