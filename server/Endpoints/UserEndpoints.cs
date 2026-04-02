using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;
using server.Services;

namespace server.Endpoints;

/// <summary>
/// User authentication endpoints: register, login, and profile retrieval.
/// </summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/user")
            .WithName("User Management")
            .WithOpenApi();

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("Register a new user account");

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Login and receive JWT token");

        group.MapGet("/profile", GetProfile)
            .WithName("Get Profile")
            .WithSummary("Get current user profile (requires authentication)");

        group.MapGet("/credits", GetCredits)
            .WithName("Get Credits")
            .WithSummary("Get current user credits balance (requires authentication)");
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        AppDbContext context,
        AuthService authService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "Email, username, and password are required." });
        }

        if (request.Password.Length < 8)
            return Results.BadRequest(new { error = "Password must be at least 8 characters." });

        var existingEmail = await context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingEmail != null)
            return Results.BadRequest(new { error = "Email already in use." });

        var existingUsername = await context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (existingUsername != null)
            return Results.BadRequest(new { error = "Username already in use." });

        var user = new User
        {
            Email = request.Email.ToLowerInvariant(),
            Username = request.Username,
            PasswordHash = AuthService.HashPassword(request.Password),
            Credits = 5 // Free credits on registration
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var token = authService.GenerateToken(user);

        return Results.Created("/api/user/profile", new
        {
            user.Id,
            user.Email,
            user.Username,
            user.Credits,
            token
        });
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        AppDbContext context,
        AuthService authService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new { error = "Email and password are required." });

        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());
        if (user == null)
            return Results.Unauthorized();

        if (!AuthService.VerifyPassword(request.Password, user.PasswordHash))
            return Results.Unauthorized();

        user.LastLoginAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var token = authService.GenerateToken(user);

        return Results.Ok(new
        {
            user.Id,
            user.Email,
            user.Username,
            user.Credits,
            token
        });
    }

    private static async Task<IResult> GetProfile(
        ClaimsPrincipal claims,
        AppDbContext context)
    {
        var userIdClaim = claims.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Results.Unauthorized();

        var user = await context.Users.FindAsync(userId);
        if (user == null)
            return Results.NotFound();

        return Results.Ok(new
        {
            user.Id,
            user.Email,
            user.Username,
            user.Credits,
            user.CreatedAt,
            user.LastLoginAt
        });
    }

    private static async Task<IResult> GetCredits(
        ClaimsPrincipal claims,
        AppDbContext context)
    {
        var userIdClaim = claims.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Results.Unauthorized();

        var user = await context.Users.FindAsync(userId);
        if (user == null)
            return Results.NotFound();

        return Results.Ok(new { credits = user.Credits });
    }
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
