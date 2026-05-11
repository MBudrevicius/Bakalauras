namespace server.Models;

public class CrossCheckResponse
{
    public string Url { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public List<RelatedPage> RelatedPages { get; set; } = [];
    public CredibilityResult? Credibility { get; set; }
    public List<SourceReliability> SourceReliability { get; set; } = [];
    public int PageLinkDomains { get; set; }
    public List<string> PageLinkSamples { get; set; } = [];
    public List<ModelCredibilityResult>? ModelResults { get; set; }
}

public class ModelCredibilityResult
{
    public string Model { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public CredibilityResult? Credibility { get; set; }
}
