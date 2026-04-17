namespace server.Models;

public class AiCheckResult
{
    public AiCheckType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int AiScore { get; set; }
}
