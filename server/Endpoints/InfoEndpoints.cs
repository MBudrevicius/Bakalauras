using server.Models;
using server.Services;

namespace server.Endpoints;

public static class InfoEndpoints
{
    public static void MapInfoEndpoints(this WebApplication app)
    {
        app.MapPost("/api/cross-check", async (CrossCheckRequest request, CrossCheckService service, HttpContext httpContext) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return Results.BadRequest(new { error = "URL is required." });

            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
                return Results.BadRequest(new { error = "Invalid URL format." });

            var claudeApiKey = httpContext.Request.Headers["X-Claude-Api-Key"].FirstOrDefault();
            var claudeModel = httpContext.Request.Headers["X-Claude-Model"].FirstOrDefault();
            var response = await service.CrossCheckAsync(request.Url, request.Title, request.Text, request.PageLinks ?? [], claudeApiKey, claudeModel);
            return Results.Ok(response);
        });

        app.MapPost("/api/cross-check/all-models", async (CrossCheckRequest request, CrossCheckService service, HttpContext httpContext) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return Results.BadRequest(new { error = "URL is required." });

            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
                return Results.BadRequest(new { error = "Invalid URL format." });

            var claudeApiKey = httpContext.Request.Headers["X-Claude-Api-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(claudeApiKey))
                return Results.BadRequest(new { error = "API key is required for all-models check." });

            var response = await service.CrossCheckAllModelsAsync(request.Url, request.Title, request.Text, request.PageLinks ?? [], claudeApiKey);
            return Results.Ok(response);
        });

        app.MapPost("/api/cross-check/highlight", async (CredibilityHighlightRequest request, CrossCheckService service, HttpContext httpContext) =>
        {
            if (request.Segments == null || request.Segments.Length == 0)
                return Results.BadRequest(new { error = "Segments array is required." });
            if (request.Segments.Length > 500)
                return Results.BadRequest(new { error = "Too many segments (max 500)." });

            var claudeApiKey = httpContext.Request.Headers["X-Claude-Api-Key"].FirstOrDefault();
            var claudeModel = httpContext.Request.Headers["X-Claude-Model"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(claudeApiKey))
                return Results.BadRequest(new { error = "API key is required for credibility highlighting." });

            var result = await service.HighlightCredibilityAsync(request.Segments, request.Topic, request.Sources, claudeApiKey, claudeModel);
            if (result == null)
                return Results.Ok(new { scores = Array.Empty<int>(), explanations = Array.Empty<string>() });

            return Results.Ok(new { scores = result.Scores, explanations = result.Explanations });
        });

        app.MapGet("/api/page-score", async (string url, PageScoreStore store) =>
        {
            if (string.IsNullOrWhiteSpace(url))
                return Results.BadRequest(new { error = "URL is required." });

            var score = await store.GetPageWithRelatedScoreAsync(url);
            if (score == null)
                return Results.Ok(new { found = false });

            return Results.Ok(new { found = true, score });
        });
    }
}
