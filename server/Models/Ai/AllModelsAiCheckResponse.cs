namespace server.Models;

public class AllModelsAiCheckResponse
{
    public int AverageAiScore { get; set; }
    public int TextLength { get; set; }
    public List<ModelResult> ModelResults { get; set; } = [];
    public List<AiCheckResult> HeuristicResults { get; set; } = [];
}

public class ModelResult
{
    public string Model { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int AiScore { get; set; }
    public int OverallAiScore { get; set; }
}
