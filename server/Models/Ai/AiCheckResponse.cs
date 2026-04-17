namespace server.Models;

public class AiCheckResponse
{
    public string AnalyzedText { get; set; } = string.Empty;
    public int TextLength { get; set; }
    public List<AiCheckResult> Results { get; set; } = [];
    public int OverallAiScore { get; set; }
}
