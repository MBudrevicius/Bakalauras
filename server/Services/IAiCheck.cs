using server.Models;

namespace server.Services;

public interface IAiCheck
{
    AiCheckType Type { get; }
    Task<AiCheckResult> RunAsync(string text);
}
