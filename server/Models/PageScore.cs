namespace server.Models;

public class PageScore
{
    public int Id { get; set; }
    public int? UserId { get; set; } // Optional: which user checked this page (null = anonymous)
    public string Url { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public int SecurityScore { get; set; }
    public int AiScore { get; set; }
    public DateTime LastChecked { get; set; }
    public int CheckCount { get; set; }

    // Navigation property
    public virtual User? User { get; set; }
}
