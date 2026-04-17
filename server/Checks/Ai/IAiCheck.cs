using server.Models;

namespace server.Checks.Ai;

public interface IAiCheck
{
    AiCheckType Type { get; }
    Task<AiCheckResult> RunAsync(string text, string? apiKey = null, string? model = null);
}
