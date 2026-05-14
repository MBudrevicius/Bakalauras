using server.Clients;
using server.Models;

namespace server.Services;

public class CrossCheckService
{
    private readonly BraveSearchClient _braveSearch;
    private readonly AnthropicClient _anthropic;
    private readonly PageScoreStore _scoreStore;

    public CrossCheckService(BraveSearchClient braveSearch, AnthropicClient anthropic, PageScoreStore scoreStore)
    {
        _braveSearch = braveSearch;
        _anthropic = anthropic;
        _scoreStore = scoreStore;
    }

    public async Task<CrossCheckResponse> CrossCheckAsync(string url, string title, string text, List<string> pageLinks, string? claudeApiKey = null, string? claudeModel = null)
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

        var sourceReliability = new List<SourceReliability>();
        if (!string.IsNullOrWhiteSpace(claudeApiKey) && !string.IsNullOrWhiteSpace(text) && relatedPages.Count > 0)
        {
            var snippets = relatedPages
                .Where(rp => !string.IsNullOrWhiteSpace(rp.Snippet))
                .Select(rp => new SourceSnippet { Title = rp.Title, Snippet = rp.Snippet })
                .ToList();

            sourceReliability = await _anthropic.EvaluateSourceReliabilityAsync(claudeApiKey, text, snippets, claudeModel);
        }

        CredibilityResult? credibility = null;
        if (!string.IsNullOrWhiteSpace(claudeApiKey) && !string.IsNullOrWhiteSpace(text) && relatedPages.Count > 0)
        {
            var reliableIndices = new HashSet<int>();
            for (var i = 0; i < sourceReliability.Count; i++)
            {
                if (sourceReliability[i].Score >= 50)
                    reliableIndices.Add(i);
            }

            var sources = relatedPages
                .Where(rp => !string.IsNullOrWhiteSpace(rp.Snippet))
                .Select((rp, idx) => new { rp, idx })
                .Where(x => reliableIndices.Count == 0 || reliableIndices.Contains(x.idx))
                .Select(x => new SourceSnippet { Title = x.rp.Title, Snippet = x.rp.Snippet })
                .ToList();

            if (sources.Count > 0)
            {
                credibility = await _anthropic.VerifyCredibilityAsync(claudeApiKey, text, sources, claudeModel);
            }
        }

        var (pageLinkDomains, pageLinkSamples) = AnalyzePageLinks(pageLinks, url);

        var response = new CrossCheckResponse
        {
            Url = url,
            Topic = topic ?? "",
            RelatedPages = relatedPages,
            Credibility = credibility,
            SourceReliability = sourceReliability,
            PageLinkDomains = pageLinkDomains,
            PageLinkSamples = pageLinkSamples,
        };

        if (credibility is not null)
        {
            await _scoreStore.SavePageScoreAsync(url, securityScore: null, credibilityScore: credibility.Score, aiScore: null);
        }

        return response;
    }

    public async Task<CrossCheckResponse> CrossCheckAllModelsAsync(string url, string title, string text, List<string> pageLinks, string claudeApiKey)
    {
        var topic = await _anthropic.ExtractTopicAsync(claudeApiKey, text, "claude-sonnet-4-6");
        topic ??= CleanTitle(title);

        var relatedPages = new List<RelatedPage>();
        if (!string.IsNullOrWhiteSpace(topic))
        {
            relatedPages = await SearchRelatedPagesAsync(topic, url);
        }

        var sourceReliability = new List<SourceReliability>();
        List<SourceSnippet> reliableSources = [];
        if (!string.IsNullOrWhiteSpace(text) && relatedPages.Count > 0)
        {
            var snippets = relatedPages
                .Where(rp => !string.IsNullOrWhiteSpace(rp.Snippet))
                .Select(rp => new SourceSnippet { Title = rp.Title, Snippet = rp.Snippet })
                .ToList();

            sourceReliability = await _anthropic.EvaluateSourceReliabilityAsync(claudeApiKey, text, snippets, "claude-sonnet-4-6");

            var reliableIndices = new HashSet<int>();
            for (var i = 0; i < sourceReliability.Count; i++)
            {
                if (sourceReliability[i].Score >= 50)
                    reliableIndices.Add(i);
            }

            reliableSources = relatedPages
                .Where(rp => !string.IsNullOrWhiteSpace(rp.Snippet))
                .Select((rp, idx) => new { rp, idx })
                .Where(x => reliableIndices.Count == 0 || reliableIndices.Contains(x.idx))
                .Select(x => new SourceSnippet { Title = x.rp.Title, Snippet = x.rp.Snippet })
                .ToList();
        }

        var models = new[]
        {
            ("claude-haiku-4-5-20251001", "Haiku 4.5"),
            ("claude-sonnet-4-6", "Sonnet 4.6"),
            ("claude-opus-4-7", "Opus 4.7")
        };

        var modelResults = new List<ModelCredibilityResult>();
        if (!string.IsNullOrWhiteSpace(text) && reliableSources.Count > 0)
        {
            var tasks = models.Select(async m =>
            {
                var cred = await _anthropic.VerifyCredibilityAsync(claudeApiKey, text, reliableSources, m.Item1);
                return new ModelCredibilityResult
                {
                    Model = m.Item1,
                    Label = m.Item2,
                    Credibility = cred
                };
            });
            modelResults = [.. await Task.WhenAll(tasks)];
        }

        var validScores = modelResults.Where(m => m.Credibility != null).Select(m => m.Credibility!.Score).ToList();
        CredibilityResult? avgCredibility = null;
        if (validScores.Count > 0)
        {
            var avgScore = (int)Math.Round(validScores.Average());
            var medianModel = modelResults
                .Where(m => m.Credibility != null)
                .OrderBy(m => m.Credibility!.Score)
                .ElementAt(validScores.Count / 2);

            avgCredibility = new CredibilityResult
            {
                Score = avgScore,
                Verdict = medianModel.Credibility!.Verdict,
                Claims = medianModel.Credibility.Claims
            };
        }

        var (pageLinkDomains, pageLinkSamples) = AnalyzePageLinks(pageLinks, url);

        var response = new CrossCheckResponse
        {
            Url = url,
            Topic = topic ?? "",
            RelatedPages = relatedPages,
            Credibility = avgCredibility,
            SourceReliability = sourceReliability,
            PageLinkDomains = pageLinkDomains,
            PageLinkSamples = pageLinkSamples,
            ModelResults = modelResults,
        };

        if (avgCredibility is not null)
        {
            await _scoreStore.SavePageScoreAsync(url, securityScore: null, credibilityScore: avgCredibility.Score, aiScore: null);
        }

        return response;
    }

    private static (int domainCount, List<string> samples) AnalyzePageLinks(List<string> pageLinks, string originalUrl)
    {
        if (pageLinks.Count == 0)
            return (0, []);

        var originalDomain = Uri.TryCreate(originalUrl, UriKind.Absolute, out var origUri)
            ? origUri.Host.ToLowerInvariant()
            : "";

        var externalDomains = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in pageLinks)
        {
            if (!Uri.TryCreate(link, UriKind.Absolute, out var linkUri))
                continue;
            if (linkUri.Scheme != "http" && linkUri.Scheme != "https")
                continue;

            var host = linkUri.Host.ToLowerInvariant();
            if (host == originalDomain)
                continue;
            if (IsCommonNonSourceDomain(host))
                continue;

            if (!externalDomains.ContainsKey(host))
                externalDomains[host] = link;
        }

        var samples = externalDomains.Values.Take(10).ToList();
        return (externalDomains.Count, samples);
    }

    private static bool IsCommonNonSourceDomain(string host)
    {
        var skip = new[] { "facebook.com", "twitter.com", "x.com", "instagram.com",
            "youtube.com", "tiktok.com", "linkedin.com", "pinterest.com",
            "google.com", "apple.com", "amazon.com", "microsoft.com" };
        return skip.Any(s => host.EndsWith(s, StringComparison.OrdinalIgnoreCase));
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

    public async Task<CredibilityHighlightResult?> HighlightCredibilityAsync(string[] segments, string topic, List<SourceSnippet> sources, string claudeApiKey, string? claudeModel = null)
    {
        if (segments.Length == 0 || string.IsNullOrWhiteSpace(claudeApiKey))
            return null;

        return await _anthropic.EvaluateSegmentCredibilityAsync(claudeApiKey, segments, topic, sources, claudeModel);
    }
}
