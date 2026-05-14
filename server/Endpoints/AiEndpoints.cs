using server.Models;
using server.Services;
namespace server.Endpoints;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ai-checks", async (
            AiCheckRequest request,
            AiCheckService service,
            HttpContext httpContext) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.Url))
            {
                return Results.BadRequest(new { error = "Either text or URL is required." });
            }

            var claudeApiKey = httpContext.Request.Headers["X-Claude-Api-Key"].FirstOrDefault();
            var claudeModel = httpContext.Request.Headers["X-Claude-Model"].FirstOrDefault();
            var response = await service.RunAllAsync(request, claudeApiKey: claudeApiKey, claudeModel: claudeModel);
            return Results.Ok(response);
        });

        app.MapPost("/api/ai-checks/all-models", async (
            AiCheckRequest request,
            AiCheckService service,
            HttpContext httpContext) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.Url))
            {
                return Results.BadRequest(new { error = "Either text or URL is required." });
            }

            var claudeApiKey = httpContext.Request.Headers["X-Claude-Api-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(claudeApiKey))
            {
                return Results.BadRequest(new { error = "API key is required for all-models check." });
            }

            var response = await service.RunAllModelsAsync(request, claudeApiKey);
            return Results.Ok(response);
        });

        app.MapPost("/api/ai-checks/highlight", async (
            HighlightRequest request,
            AiCheckService service,
            HttpContext httpContext) =>
        {
            if (request.Segments == null || request.Segments.Length == 0)
            {
                return Results.BadRequest(new { error = "Segments are required." });
            }

            if (request.Segments.Length > 500)
            {
                return Results.BadRequest(new { error = "Too many segments (max 500)." });
            }

            var claudeApiKey = httpContext.Request.Headers["X-Claude-Api-Key"].FirstOrDefault();
            var claudeModel = httpContext.Request.Headers["X-Claude-Model"].FirstOrDefault();
            var scores = await service.AnalyzeSegmentsAsync(request.Segments, claudeApiKey, claudeModel);
            return Results.Ok(new { scores });
        });
    }
}
