namespace server.Models;

/// <summary>
/// Confidence that content is AI-generated, as a 0–100 score.
/// </summary>
public class AiCheckResult
{
    public AiCheckType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>0 = certainly human, 100 = certainly AI.</summary>
    public int AiScore { get; set; }
}
