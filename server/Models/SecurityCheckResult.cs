namespace server.Models;

public class SecurityCheckResult
{
    public SecurityCheckType Type { get; set; }
    public SecurityCheckSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
