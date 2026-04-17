namespace server.Models;

public class SecurityCheckResponse
{
    public string Url { get; set; } = string.Empty;
    public List<SecurityCheckResult> Results { get; set; } = [];
    public int OverallScore { get; set; }
}
