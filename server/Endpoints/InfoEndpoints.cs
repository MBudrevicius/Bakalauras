using server.Models;
using server.Services;

namespace server.Endpoints;

public static class InfoEndpoints
{
    public static void MapInfoEndpoints(this WebApplication app)
    {
        app.MapPost("/api/cross-check", async (CrossCheckRequest request, CrossCheckService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return Results.BadRequest(new { error = "URL is required." });

            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
                return Results.BadRequest(new { error = "Invalid URL format." });

            var response = await service.CrossCheckAsync(request.Url);
            return Results.Ok(response);
        });

        app.MapGet("/api/page-score", async (string url, PageScoreStore store) =>
        {
            if (string.IsNullOrWhiteSpace(url))
                return Results.BadRequest(new { error = "URL is required." });

            var score = await store.GetAsync(url);
            if (score == null)
                return Results.Ok(new { found = false });

            return Results.Ok(new { found = true, score });
        });
    }
}
