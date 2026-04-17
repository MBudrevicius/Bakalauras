using server.Clients;
using server.Models;

namespace server.Services;

public class CrossCheckService
{
    private readonly BraveSearchClient _braveSearch;
    private readonly AnthropicClient _anthropic;

    public CrossCheckService(BraveSearchClient braveSearch, AnthropicClient anthropic)
    {
        _braveSearch = braveSearch;
        _anthropic = anthropic;
    }

    public async Task<CrossCheckResponse> CrossCheckAsync(string url, string title, string text, string? claudeApiKey = null, string? claudeModel = null)
    {
        string? topic = null;
        if (!string.IsNullOrWhiteSpace(claudeApiKey) && !string.IsNullOrWhiteSpace(text))
        {
            topic = await _anthropic.ExtractTopicAsync(claudeApiKey, text, claudeModel);
        }
        topic ??= CleanTitle(title);

        var relatedPages = new List<RelatedPage>();

        if (!string.IsNullOrWhiteSpace(topic))
        {
            relatedPages = await SearchRelatedPagesAsync(topic, url);
        }

        return new CrossCheckResponse
        {
            Url = url,
            Topic = topic ?? "",
            RelatedPages = relatedPages,
        };
    }

    private static string CleanTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";

        var separators = new[] { " - ", " | ", " \u2013 ", " \u2014 ", " :: " };
        foreach (var sep in separators)
        {
            var idx = title.LastIndexOf(sep, StringComparison.Ordinal);
            if (idx > 10)
            {
                title = title[..idx].Trim();
                break;
            }
        }
        return title.Trim();
    }

    private async Task<List<RelatedPage>> SearchRelatedPagesAsync(string topic, string originalUrl)
    {
        var results = await _braveSearch.SearchAsync(topic);

        var originalDomain = Uri.TryCreate(originalUrl, UriKind.Absolute, out var origUri)
            ? origUri.Host.ToLowerInvariant()
            : "";

        return [.. results
            .Where(r =>
            {
                if (!Uri.TryCreate(r.Url, UriKind.Absolute, out var linkUri)) return true;
                return !linkUri.Host.Equals(originalDomain, StringComparison.InvariantCultureIgnoreCase);
            })
            .Select(r => new RelatedPage
            {
                Url = r.Url,
                Title = r.Title,
                Snippet = r.Snippet,
            })];
    }
}
