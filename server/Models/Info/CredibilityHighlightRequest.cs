namespace server.Models;

public class CredibilityHighlightRequest
{
    public string[] Segments { get; set; } = [];
    public string Topic { get; set; } = string.Empty;
    public List<SourceSnippet> Sources { get; set; } = [];
}

public class CredibilityHighlightResult
{
    public int[] Scores { get; set; } = [];
    public string[] Explanations { get; set; } = [];
}
