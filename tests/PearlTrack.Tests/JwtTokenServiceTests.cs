using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PearlTrack.API.Models;
using PearlTrack.API.Services;

namespace PearlTrack.Tests;

public class JwtTokenServiceTests
{
    private readonly ITokenService _tokenService;
    private readonly Mock<IConfiguration> _configMock;

    public JwtTokenServiceTests()
    {
        _configMock = new Mock<IConfiguration>();
        _configMock
            .Setup(x => x[It.Is<string>(s => s == "Jwt:SecretKey")])
            .Returns("test-super-secret-key-at-least-32-characters-long-for-testing!!!");
        _configMock
            .Setup(x => x[It.Is<string>(s => s == "Jwt:Issuer")])
            .Returns("TestAPI");
        _configMock
            .Setup(x => x[It.Is<string>(s => s == "Jwt:Audience")])
            .Returns("TestClient");
        _configMock
            .Setup(x => x[It.Is<string>(s => s == "Jwt:ExpirationMinutes")])
            .Returns("60");

        var loggerMock = new Mock<ILogger<JwtTokenService>>();
        _tokenService = new JwtTokenService(_configMock.Object, loggerMock.Object);
    }

    [Fact]
    public void GenerateToken_WithValidUser_ReturnsValidToken()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-id",
            UserName = "testuser",
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "User"
        };
        var roles = new List<string> { "Admin", "User" };

        // Act
        var token = _tokenService.GenerateToken(user, roles);

        // Assert
        Assert.NotEmpty(token);
        
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
        
        Assert.NotNull(jsonToken);
        Assert.Equal("TestAPI", jsonToken!.Issuer);
        Assert.Contains(jsonToken.Audiences, a => a == "TestClient");
    }

    [Fact]
    public void GenerateToken_ContainsUserClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-id-123",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };
        var roles = new List<string> { "User" };

        // Act
        var token = _tokenService.GenerateToken(user, roles);
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        Assert.NotNull(jsonToken);
        
        // Verify key claims are present - check by value existence rather than type
        var claims = jsonToken!.Claims.ToList();
        Assert.Contains(claims, c => c.Value == "test-id-123");
        Assert.Contains(claims, c => c.Value == "testuser");
        Assert.Contains(claims, c => c.Value == "test@example.com");
        Assert.Contains(claims, c => c.Value == "John");
        Assert.Contains(claims, c => c.Value == "Doe");
    }

    [Fact]
    public void GenerateToken_ContainsRoleClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-id",
            UserName = "testuser",
            Email = "test@test.com"
        };
        var roles = new List<string> { "Admin", "Moderator", "User" };

        // Act
        var token = _tokenService.GenerateToken(user, roles);
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        Assert.NotNull(jsonToken);
        
        // Verify all roles are present in the token
        var claimsValues = jsonToken!.Claims.Select(c => c.Value).ToList();
        Assert.Contains("Admin", claimsValues);
        Assert.Contains("Moderator", claimsValues);
        Assert.Contains("User", claimsValues);
    }

    [Fact]
    public void GenerateToken_WithEmptyRoles_StillGeneratesToken()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-id",
            UserName = "testuser",
            Email = "test@test.com"
        };
        var roles = new List<string>();

        // Act
        var token = _tokenService.GenerateToken(user, roles);

        // Assert
        Assert.NotEmpty(token);
        
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
        Assert.NotNull(jsonToken);
    }

    [Fact]
    public void GenerateToken_TokenExpiresInConfiguredTime()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-id",
            UserName = "testuser",
            Email = "test@test.com"
        };
        var roles = new List<string>();

        // Act
        var token = _tokenService.GenerateToken(user, roles);
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

        // Assert
        Assert.NotNull(jsonToken?.ValidTo);
        // Token should expire within 60 minutes (configured)
        var expirationMinutes = (jsonToken!.ValidTo - DateTime.UtcNow).TotalMinutes;
        Assert.InRange(expirationMinutes, 59, 61); // Allow 1 minute variance
    }
}
