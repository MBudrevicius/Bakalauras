using server.Models;

namespace server.Services;

public interface ISecurityCheck
{
    SecurityCheckType Type { get; }
    Task<SecurityCheckResult> RunAsync(string url);
}
