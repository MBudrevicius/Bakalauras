using server.Models;
using server.Services;

namespace server.Endpoints;

public static class SecurityEndpoints
{
    public static void MapSecurityEndpoints(this WebApplication app)
    {
        app.MapPost("/api/security-checks", async (SecurityCheckRequest request, SecurityCheckService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return Results.BadRequest(new { error = "URL is required." });

            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
                return Results.BadRequest(new { error = "Invalid URL format." });

            var response = await service.RunAllAsync(request.Url);
            return Results.Ok(response);
        });
    }
}
