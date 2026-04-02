namespace server.Models;

public class AiCheckResponse
{
    public string AnalyzedText { get; set; } = string.Empty;
    public int TextLength { get; set; }
    public List<AiCheckResult> Results { get; set; } = [];

    /// <summary>Weighted overall AI probability 0–100.</summary>
    public int OverallAiScore { get; set; }
}
