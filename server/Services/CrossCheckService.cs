using System.Text.Json;
using System.Web;
using HtmlAgilityPack;
using server.Models;

namespace server.Services;

/// <summary>
/// Finds related pages for a given URL by extracting the page topic
/// and searching via Google Custom Search JSON API.
/// Requires GoogleCustomSearch:ApiKey and GoogleCustomSearch:Cx in config.
/// </summary>
public class CrossCheckService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly PageScoreStore _scoreStore;

    public CrossCheckService(IConfiguration config, IHttpClientFactory httpFactory, PageScoreStore scoreStore)
    {
        _config = config;
        _httpFactory = httpFactory;
        _scoreStore = scoreStore;
    }

    public async Task<CrossCheckResponse> CrossCheckAsync(string url, int? userId = null)
    {
        var topic = await ExtractTopicAsync(url);
        var relatedPages = new List<RelatedPage>();

        if (!string.IsNullOrWhiteSpace(topic))
        {
            relatedPages = await SearchRelatedPagesAsync(topic, url);
        }

        // Look up stored score for the requested page
        var pageScore = await _scoreStore.GetAsync(url, userId);

        // Calculate adjusted scores using related-page influence
        int? adjSec = null, adjAi = null;
        if (pageScore != null)
        {
            var relSecScores = relatedPages
                .Where(r => r.SecurityScore.HasValue)
                .Select(r => r.SecurityScore!.Value);
            var relAiScores = relatedPages
                .Where(r => r.AiScore.HasValue)
                .Select(r => r.AiScore!.Value);

            adjSec = PageScoreStore.CalculateAdjustedScore(pageScore.SecurityScore, relSecScores);
            adjAi = PageScoreStore.CalculateAdjustedScore(pageScore.AiScore, relAiScores);
        }

        return new CrossCheckResponse
        {
            Url = url,
            Topic = topic,
            RelatedPages = relatedPages,
            PageScore = pageScore,
            AdjustedSecurityScore = adjSec,
            AdjustedAiScore = adjAi
        };
    }

    private async Task<string> ExtractTopicAsync(string url)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; WebChecker/0.1)");

            var html = await client.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Try <title> first, then <h1>
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                // Strip common suffixes like " - SiteName", " | SiteName"
                var separators = new[] { " - ", " | ", " – ", " — ", " :: " };
                foreach (var sep in separators)
                {
                    var idx = title.LastIndexOf(sep, StringComparison.Ordinal);
                    if (idx > 10) // keep enough of the title
                    {
                        title = title[..idx].Trim();
                        break;
                    }
                }
                return HttpUtility.HtmlDecode(title);
            }

            var h1 = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(h1))
                return HttpUtility.HtmlDecode(h1);

            // Fallback: use the domain + path
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return uri.Host + uri.AbsolutePath;

            return "";
        }
        catch
        {
            // Fallback: use domain from URL
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return uri.Host;
            return "";
        }
    }

    private async Task<List<RelatedPage>> SearchRelatedPagesAsync(string topic, string originalUrl)
    {
        var apiKey = _config["GoogleCustomSearch:ApiKey"];
        var cx = _config["GoogleCustomSearch:Cx"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(cx))
            return [];

        try
        {
            var client = _httpFactory.CreateClient();
            var query = HttpUtility.UrlEncode(topic);
            var requestUrl = $"https://www.googleapis.com/customsearch/v1?key={apiKey}&cx={cx}&q={query}&num=8";

            var response = await client.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var results = new List<RelatedPage>();

            if (!doc.RootElement.TryGetProperty("items", out var items))
                return results;

            // Get the domain of the original URL to exclude self-references
            var originalDomain = Uri.TryCreate(originalUrl, UriKind.Absolute, out var origUri)
                ? origUri.Host.ToLowerInvariant()
                : "";

            foreach (var item in items.EnumerateArray())
            {
                var link = item.GetProperty("link").GetString() ?? "";
                var title = item.GetProperty("title").GetString() ?? "";
                var snippet = item.TryGetProperty("snippet", out var sn) ? sn.GetString() ?? "" : "";

                // Skip results from the same domain
                if (Uri.TryCreate(link, UriKind.Absolute, out var linkUri)
                    && linkUri.Host.ToLowerInvariant() == originalDomain)
                    continue;

                // Look up our stored scores for this result
                var stored = await _scoreStore.GetAsync(link);

                results.Add(new RelatedPage
                {
                    Url = link,
                    Title = HttpUtility.HtmlDecode(title),
                    Snippet = HttpUtility.HtmlDecode(snippet),
                    SecurityScore = stored?.SecurityScore,
                    AiScore = stored?.AiScore
                });
            }

            return results;
        }
        catch
        {
            return [];
        }
    }
}
