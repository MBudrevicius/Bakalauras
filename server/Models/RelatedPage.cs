namespace server.Models;

public class RelatedPage
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;

    /// <summary>Our stored security score, or null if never checked.</summary>
    public int? SecurityScore { get; set; }

    /// <summary>Our stored AI score, or null if never checked.</summary>
    public int? AiScore { get; set; }
}
