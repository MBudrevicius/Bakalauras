using server.Models;

namespace server.Checks.Security;

public interface ISecurityCheck
{
    SecurityCheckType Type { get; }
    Task<SecurityCheckResult> RunAsync(string url);
}
