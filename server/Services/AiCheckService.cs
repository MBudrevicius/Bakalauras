using HtmlAgilityPack;
using server.Models;

namespace server.Services;

public class AiCheckService
{
    private readonly IEnumerable<IAiCheck> _checks;
    private readonly PageScoreStore _scoreStore;

    public AiCheckService(IEnumerable<IAiCheck> checks, PageScoreStore scoreStore)
    {
        _checks = checks;
        _scoreStore = scoreStore;
    }

    public async Task<AiCheckResponse> RunAllAsync(AiCheckRequest request)
    {
        var text = request.Text;

        // If no text provided but URL given, extract text from the page
        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(request.Url))
        {
            text = await ExtractTextFromUrl(request.Url);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new AiCheckResponse
            {
                AnalyzedText = "",
                TextLength = 0,
                Results = [],
                OverallAiScore = 0
            };
        }

        var tasks = _checks.Select(c => c.RunAsync(text));
        var results = await Task.WhenAll(tasks);

        var response = new AiCheckResponse
        {
            AnalyzedText = text.Length > 500 ? text[..500] + "\u2026" : text,
            TextLength = text.Length,
            Results = results.ToList(),
            OverallAiScore = CalculateOverallScore(results)
        };

        // Persist the AI score if we have a URL
        if (!string.IsNullOrWhiteSpace(request.Url))
            await _scoreStore.SaveAsync(request.Url, securityScore: null, aiScore: response.OverallAiScore);

        return response;
    }

    private static int CalculateOverallScore(AiCheckResult[] results)
    {
        if (results.Length == 0) return 0;

        // Weighted average — Claude model gets more weight when available
        var totalWeight = 0;
        var weightedSum = 0;

        foreach (var r in results)
        {
            var weight = r.Type == AiCheckType.ClaudeAiModel && r.AiScore > 0 ? 3 : 1;
            totalWeight += weight;
            weightedSum += r.AiScore * weight;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0;
    }

    private static async Task<string> ExtractTextFromUrl(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; SecurityChecker/0.1)");
            var html = await client.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove script and style elements
            foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//noscript") ?? Enumerable.Empty<HtmlNode>())
                node.Remove();

            var text = doc.DocumentNode.InnerText;

            // Collapse whitespace
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }
        catch
        {
            return "";
        }
    }
}
