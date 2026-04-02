using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;
using server.Services;

namespace server.Endpoints;

public static class AiEndpoints
{
    // Cost in credits for Claude AI model check (0 for other checks)
    private const decimal CLAUDE_AI_COST = 1;

    public static void MapAiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ai-checks", async (
            AiCheckRequest request,
            ClaimsPrincipal claims,
            AiCheckService service,
            AppDbContext context) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.Url))
                return Results.BadRequest(new { error = "Either text or URL is required." });

            // Check if user is authenticated
            var userIdClaim = claims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var user = await context.Users.FindAsync(userId);
            if (user == null)
                return Results.NotFound();

            // Check if user wants to use Claude AI (optional paid feature)
            if (request.UseClaudeAi && user.Credits < CLAUDE_AI_COST)
            {
                return Results.BadRequest(new
                {
                    error = "Insufficient credits for Claude AI check.",
                    creditsNeeded = CLAUDE_AI_COST,
                    creditsAvailable = user.Credits
                });
            }

            var response = await service.RunAllAsync(request, userId: userId);

            // Deduct credits only if Claude AI was used
            if (request.UseClaudeAi && response.Results.Any(r => r.Type == AiCheckType.ClaudeAiModel && r.AiScore > 0))
            {
                user.Credits -= CLAUDE_AI_COST;
                await context.SaveChangesAsync();
            }

            return Results.Ok(new
            {
                response,
                creditsRemaining = user.Credits,
                claudeAiUsed = request.UseClaudeAi
            });
        });

        // Endpoint for anonymous/free checks (no authentication required)
        app.MapPost("/api/ai-checks/free", async (
            AiCheckRequest request,
            AiCheckService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.Url))
                return Results.BadRequest(new { error = "Either text or URL is required." });

            var response = await service.RunAllAsync(request, userId: null);
            return Results.Ok(response);
        });
    }
}
