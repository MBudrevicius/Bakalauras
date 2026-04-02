namespace server.Models;

public class PageScore
{
    public string Url { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public int SecurityScore { get; set; }
    public int AiScore { get; set; }
    public DateTime LastChecked { get; set; }
    public int CheckCount { get; set; }
}
