namespace server.Models;

public class CrossCheckResponse
{
    public string Url { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public List<RelatedPage> RelatedPages { get; set; } = [];
}
