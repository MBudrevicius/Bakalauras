using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace server.Helpers;

public partial class HtmlTextExtractor
{
    private readonly IHttpClientFactory _httpFactory;
    private const int MaxResponseBytes = 2 * 1024 * 1024; // 2 MB

    public HtmlTextExtractor(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    private async Task<string> FetchHtmlAsync(string url)
    {
        var client = _httpFactory.CreateClient("HtmlFetcher");
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; SecureWeb/0.1)");
        client.MaxResponseContentBufferSize = MaxResponseBytes;

        return await client.GetStringAsync(url);
    }

    public async Task<string> ExtractTextFromUrl(string url)
    {
        try
        {
            var html = await FetchHtmlAsync(url);
            return ExtractTextFromHtml(html);
        }
        catch
        {
            return "";
        }
    }

    public async Task<List<string>> ExtractLinksFromUrl(string url)
    {
        try
        {
            var html = await FetchHtmlAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var sourceDomain = Uri.TryCreate(url, UriKind.Absolute, out var sourceUri)
                ? sourceUri.Host.ToLowerInvariant()
                : "";

            var links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchors == null) return [];

            foreach (var anchor in anchors)
            {
                var href = anchor.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href)) continue;
                if (!Uri.TryCreate(href, UriKind.Absolute, out var linkUri))
                {
                    if (Uri.TryCreate(sourceUri, href, out linkUri) == false)
                    {
                        continue;
                    }
                }
                if (linkUri.Scheme is not ("http" or "https")) continue;
                if (linkUri.Host.Equals(sourceDomain, StringComparison.InvariantCultureIgnoreCase)) continue;

                links.Add(linkUri.GetLeftPart(UriPartial.Path));
            }

            return [.. links];
        }
        catch
        {
            return [];
        }
    }

    private static string ExtractTextFromHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//noscript|//svg|//iframe|//canvas") ?? Enumerable.Empty<HtmlNode>())
        {
            node.Remove();
        }

        var text = HttpUtility.HtmlDecode(doc.DocumentNode.InnerText);
        text = WhitespaceRegex().Replace(text, " ").Trim();
        return text;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
