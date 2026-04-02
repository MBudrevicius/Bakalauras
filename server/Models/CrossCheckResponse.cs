namespace server.Models;

public class CrossCheckResponse
{
    public string Url { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public List<RelatedPage> RelatedPages { get; set; } = [];

    /// <summary>Stored page score (direct checks), or null if never checked.</summary>
    public PageScore? PageScore { get; set; }

    /// <summary>Security score adjusted by related-page influence.</summary>
    public int? AdjustedSecurityScore { get; set; }

    /// <summary>AI score adjusted by related-page influence.</summary>
    public int? AdjustedAiScore { get; set; }
}
