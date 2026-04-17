using System.Text;
using System.Text.Json;

namespace server.Clients;

public class AnthropicClient
{
    private readonly IHttpClientFactory _httpFactory;

    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string DefaultModel = "claude-haiku-4-5-20251001";
    private static readonly HashSet<string> AllowedModels = ["claude-haiku-4-5-20251001", "claude-sonnet-4-6", "claude-opus-4-7"];
    private const int MaxTextLength = 4000;

    public AnthropicClient(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<int> DetectAiTextAsync(string apiKey, string text, string? model = null)
    {
        var sample = text.Length > MaxTextLength ? text[..MaxTextLength] : text;

        var prompt = $"""
            You are an AI-generated text detector. Analyze the following text and estimate the probability (0-100) that it was written by an AI language model rather than a human.
            Reply with ONLY a single integer between 0 and 100, nothing else. 0 means certainly human, 100 means certainly AI.
            Text to analyze:
            ---
            {sample}
            ---
            """;

        var reply = await SendMessageAsync(apiKey, prompt, model: model);
        return ParseScore(reply);
    }

    public async Task<int[]?> DetectAiSegmentsAsync(string apiKey, string[] segments, string? model = null)
    {
        if (segments.Length == 0)
        {
            return null;
        }

        try
        {
            var sb = new StringBuilder();
            for (var i = 0; i < segments.Length; i++)
            {
                var text = segments[i].Length > 300 ? segments[i][..300] : segments[i];
                sb.AppendLine($"[{i}] {text}");
            }

            var paragraphs = sb.ToString();
            if (paragraphs.Length > 12000)
            {
                paragraphs = paragraphs[..12000];
            }

            var prompt = $"""
                You are an AI-generated text detector. For each numbered paragraph below, estimate the probability (0-100) that it was written by an AI language model.
                Reply with ONLY one score per line in the format: [number] score
                Example: [0] 75
                Do not add any other text.
                Paragraphs:
                {paragraphs}
                """;

            var reply = await SendMessageAsync(apiKey, prompt, Math.Min(segments.Length * 8, 2048), model);
            return ParseSegmentScores(reply, segments.Length);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> ExtractTopicAsync(string apiKey, string text, string? model = null)
    {
        var sample = text.Length > MaxTextLength ? text[..MaxTextLength] : text;

        var prompt = $"""
            You are a fact-checking assistant. Analyze the following web page text and identify the single most specific, verifiable claim or topic it covers.
            Your goal is to produce a search query that would find other independent sources covering the same subject, so the user can cross-check the information.
            Rules:
            - Focus on the core factual claim, event, or subject — not meta-commentary or opinions.
            - Include key specifics: names, dates, locations, or numbers when present.
            - Keep it 5-12 words, suitable as a web search query.
            - Reply with ONLY the search query, nothing else.
            Text:
            ---
            {sample}
            ---
            """;

        try
        {
            var reply = await SendMessageAsync(apiKey, prompt, 50, model);
            var topic = reply.Trim().Trim('"', '\'');
            return string.IsNullOrWhiteSpace(topic) ? null : topic;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> SendMessageAsync(string apiKey, string prompt, int maxTokens = 60, string? model = null)
    {
        var resolvedModel = AllowedModels.Contains(model ?? "") ? model! : DefaultModel;
        var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var requestBody = new
        {
            model = resolvedModel,
            max_tokens = maxTokens,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(Endpoint, content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Anthropic API returned {(int)response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "";
    }

    private static int ParseScore(string reply)
    {
        var cleaned = reply.Trim();
        if (int.TryParse(cleaned, out var score))
        {
            return Math.Clamp(score, 0, 100);
        }

        foreach (var word in cleaned.Split(' ', '\n', '\r', '\t', '.', ',', '%'))
        {
            if (int.TryParse(word.Trim(), out var parsed))
            {
                return Math.Clamp(parsed, 0, 100);
            }
        }

        throw new InvalidOperationException($"Could not parse score from Claude response: {cleaned}");
    }

    private static int[] ParseSegmentScores(string reply, int count)
    {
        var scores = new int[count];
        foreach (var line in reply.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('[')) continue;

            var closeBracket = trimmed.IndexOf(']');
            if (closeBracket < 0) continue;

            if (int.TryParse(trimmed[1..closeBracket], out var idx)
                && idx >= 0 && idx < count)
            {
                var rest = trimmed[(closeBracket + 1)..].Trim();
                foreach (var word in rest.Split(' ', '\t', ',', '%'))
                {
                    if (int.TryParse(word.Trim(), out var score))
                    {
                        scores[idx] = Math.Clamp(score, 0, 100);
                        break;
                    }
                }
            }
        }

        return scores;
    }
}
