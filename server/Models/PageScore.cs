namespace server.Models;

public class PageScore
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public int SecurityScore { get; set; }
    public int CredibilityScore { get; set; }
    public int AiScore { get; set; }
    public int SecurityCheckCount { get; set; }
    public int CredibilityCheckCount { get; set; }
    public int AiCheckCount { get; set; }
    public DateTime LastChecked { get; set; }
    public int CheckCount { get; set; }
}
