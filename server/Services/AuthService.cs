using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using server.Models;

namespace server.Services;

/// <summary>
/// Handles user authentication including JWT token generation and password hashing.
/// </summary>
public class AuthService
{
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly int _jwtExpirationMinutes;

    public AuthService(IConfiguration config)
    {
        _jwtSecret = config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        _jwtIssuer = config["Jwt:Issuer"] ?? "YourAppName";
        _jwtAudience = config["Jwt:Audience"] ?? "YourAppUsers";
        _jwtExpirationMinutes = int.Parse(config["Jwt:ExpirationMinutes"] ?? "1440");

        if (_jwtSecret.Length < 32)
            throw new InvalidOperationException("JWT Secret must be at least 32 characters");
    }

    /// <summary>Hash a password using BCrypt.</summary>
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    /// <summary>Verify a password against its hash.</summary>
    public static bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    /// <summary>Generate a JWT token for a user.</summary>
    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
