using server.Models;
using server.Services;

namespace server.Endpoints;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ai-checks", async (AiCheckRequest request, AiCheckService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.Url))
                return Results.BadRequest(new { error = "Either text or URL is required." });

            var response = await service.RunAllAsync(request);
            return Results.Ok(response);
        });
    }
}
