using System.Text;
using System.Text.Json;
using server.Models;

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
            You are a precision fact-checking assistant. Your task is to extract the EXACT central factual claim from the article below and turn it into an optimal search query.

            Your goal: produce a search query that will find independent news/research sources covering the SAME specific event, study, or claim — not just the general topic.

            Rules:
            - Identify the single most important FACTUAL claim (not opinions, not meta-commentary).
            - Include ALL key specifics: full proper names, exact dates, specific numbers, locations.
            - Use quotation marks around proper nouns or exact phrases if they help precision.
            - The query should be 5-15 words, optimized for a news/web search engine.
            - If the article makes a surprising or extraordinary claim, include the specific claim in the query.
            - Do NOT generalize — "climate change effects" is bad; "2026 Arctic ice extent record low March" is good.
            - Reply with ONLY the search query, nothing else.

            Text:
            ---
            {sample}
            ---
            """;

        try
        {
            var reply = await SendMessageAsync(apiKey, prompt, 60, model);
            var topic = reply.Trim().Trim('"', '\'');
            return string.IsNullOrWhiteSpace(topic) ? null : topic;
        }
        catch
        {
            return null;
        }
    }

    public async Task<CredibilityResult?> VerifyCredibilityAsync(string apiKey, string pageText, List<SourceSnippet> sources, string? model = null)
    {
        if (sources.Count == 0)
            return null;

        var sample = pageText.Length > MaxTextLength ? pageText[..MaxTextLength] : pageText;

        var sb = new StringBuilder();
        foreach (var src in sources.Take(6))
        {
            var snippet = src.Snippet.Length > 400 ? src.Snippet[..400] : src.Snippet;
            sb.AppendLine($"- [{src.Title}]: {snippet}");
        }

        var prompt = $"""
            You are an expert fact-checker and misinformation analyst. Your job is to rigorously evaluate whether the ARTICLE below contains accurate information, misinformation, or deliberate lies.

            Analyze the article against the INDEPENDENT SOURCES. Look for:
            1. FACTUAL ACCURACY: Do the article's specific claims (numbers, dates, quotes, events) match what independent sources report?
            2. MISINFORMATION INDICATORS: Misleading framing, cherry-picked data, out-of-context quotes, false causation, exaggeration of facts.
            3. MANIPULATION TACTICS: Emotional language designed to mislead, conspiracy framing, false equivalence, appeal to fear without evidence.
            4. OMISSION: Does the article leave out critical context that would change the reader's understanding?
            5. SOURCE AGREEMENT: Do independent sources corroborate or contradict the article's narrative?

            Reply in EXACTLY this format (no extra text):
            SCORE: <integer 0-100>
            VERDICT: <one of: Supported | Mostly Supported | Mixed | Mostly Unsupported | Unsupported | Unverifiable>
            CLAIMS:
            - <claim 1>: <Supported|Contradicted|Unverifiable|Misleading> - <short reason>
            - <claim 2>: <Supported|Contradicted|Unverifiable|Misleading> - <short reason>
            - <claim 3>: <Supported|Contradicted|Unverifiable|Misleading> - <short reason>

            Scoring guide:
            - 90-100: All key claims fully supported by multiple independent sources.
            - 70-89: Mostly accurate with minor unsupported details.
            - 45-69: Mixed — some claims supported, others contradicted or misleading.
            - 20-44: Mostly inaccurate or significantly misleading.
            - 0-19: Contains clear disinformation or fabricated claims.

            Rules:
            - List 3-6 key factual claims from the article.
            - "Misleading" = technically true but framed to deceive or missing critical context.
            - Be skeptical of extraordinary claims lacking strong source support.
            - If sources unanimously contradict a claim, it is likely false.
            - If the article uses emotional/sensational language without factual backing, lower the score.
            - Be objective. Do not favor any political or ideological position.

            ARTICLE:
            ---
            {sample}
            ---

            INDEPENDENT SOURCES:
            {sb}
            """;

        try
        {
            var reply = await SendMessageAsync(apiKey, prompt, 700, model);
            return ParseCredibilityResult(reply);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<SourceReliability>> EvaluateSourceReliabilityAsync(string apiKey, string articleText, List<SourceSnippet> sources, string? model = null)
    {
        if (sources.Count == 0)
            return [];

        var sample = articleText.Length > MaxTextLength ? articleText[..MaxTextLength] : articleText;

        var sb = new StringBuilder();
        for (var i = 0; i < Math.Min(sources.Count, 8); i++)
        {
            var src = sources[i];
            var snippet = src.Snippet.Length > 300 ? src.Snippet[..300] : src.Snippet;
            sb.AppendLine($"[{i}] {src.Title} | {snippet}");
        }

        var prompt = $"""
            You are an expert source evaluator. Given the ARTICLE topic and a list of SOURCES found via search, rate how relevant and reliable each source is for verifying the article's claims.

            For each source, provide a score (0-100) where:
            - 90-100: Highly relevant, authoritative source directly covering the same topic
            - 70-89: Relevant source covering the topic with minor differences
            - 45-69: Partially relevant, covers related but not identical topic
            - 20-44: Marginally relevant, tangential connection
            - 0-19: Irrelevant or unreliable for this topic

            Reply with ONLY one score per line in format: [number] score
            Example: [0] 85
            Do not add any other text.

            ARTICLE (topic context):
            ---
            {sample[..Math.Min(sample.Length, 1500)]}
            ---

            SOURCES:
            {sb}
            """;

        try
        {
            var reply = await SendMessageAsync(apiKey, prompt, Math.Min(sources.Count * 10, 200), model);
            var scores = ParseSegmentScores(reply, Math.Min(sources.Count, 8));
            if (scores == null) return [];

            var results = new List<SourceReliability>();
            for (var i = 0; i < Math.Min(scores.Length, sources.Count); i++)
            {
                results.Add(new SourceReliability
                {
                    Title = sources[i].Title,
                    Score = scores[i]
                });
            }
            return results;
        }
        catch
        {
            return [];
        }
    }

    public async Task<CredibilityHighlightResult?> EvaluateSegmentCredibilityAsync(string apiKey, string[] segments, string topic, List<SourceSnippet> sources, string? model = null)
    {
        if (segments.Length == 0 || sources.Count == 0)
            return null;

        try
        {
            var sourceSb = new StringBuilder();
            foreach (var src in sources.Take(4))
            {
                var snippet = src.Snippet.Length > 300 ? src.Snippet[..300] : src.Snippet;
                sourceSb.AppendLine($"- [{src.Title}]: {snippet}");
            }

            var segSb = new StringBuilder();
            for (var i = 0; i < segments.Length; i++)
            {
                var text = segments[i].Length > 300 ? segments[i][..300] : segments[i];
                segSb.AppendLine($"[{i}] {text}");
            }

            var paragraphs = segSb.ToString();
            if (paragraphs.Length > 10000)
                paragraphs = paragraphs[..10000];

            var prompt = $"""
                You are an expert fact-checker. For each numbered paragraph below, evaluate its credibility/accuracy based on the TOPIC and INDEPENDENT SOURCES provided.

                For each paragraph, provide:
                - A credibility score (0-100): 100 = fully supported by sources, 0 = clearly false/misinformation
                - A brief explanation (max 15 words) of why it scores that way

                Scoring guide:
                - 80-100: Claim is supported by independent sources or is verifiably factual
                - 60-79: Mostly accurate but contains minor unsupported details
                - 40-59: Mixed — partially true but misleading or missing key context
                - 20-39: Mostly inaccurate or significantly misleading
                - 0-19: Contains clear misinformation contradicted by sources
                - If a paragraph is opinion/non-factual (e.g. greeting, transition), score 75

                Reply with ONLY one line per paragraph in format: [number] score | explanation
                Example: [0] 85 | Supported by multiple news sources
                Example: [1] 25 | Contradicts WHO data from 2025
                Do not add any other text.

                TOPIC: {topic}

                INDEPENDENT SOURCES:
                {sourceSb}

                PARAGRAPHS TO EVALUATE:
                {paragraphs}
                """;

            var reply = await SendMessageAsync(apiKey, prompt, Math.Min(segments.Length * 20, 3000), model);
            return ParseCredibilityHighlightResult(reply, segments.Length);
        }
        catch
        {
            return null;
        }
    }

    private static CredibilityHighlightResult ParseCredibilityHighlightResult(string reply, int count)
    {
        var scores = new int[count];
        var explanations = new string[count];
        Array.Fill(scores, 75); // default: neutral
        Array.Fill(explanations, "");

        foreach (var line in reply.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('[')) continue;

            var closeBracket = trimmed.IndexOf(']');
            if (closeBracket < 0) continue;

            if (int.TryParse(trimmed[1..closeBracket], out var idx) && idx >= 0 && idx < count)
            {
                var rest = trimmed[(closeBracket + 1)..].Trim();
                var pipeIdx = rest.IndexOf('|');

                if (pipeIdx > 0)
                {
                    var scorePart = rest[..pipeIdx].Trim();
                    var explPart = rest[(pipeIdx + 1)..].Trim();

                    foreach (var word in scorePart.Split(' ', '\t', ',', '%'))
                    {
                        if (int.TryParse(word.Trim(), out var score))
                        {
                            scores[idx] = Math.Clamp(score, 0, 100);
                            break;
                        }
                    }
                    explanations[idx] = explPart;
                }
                else
                {
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
        }

        return new CredibilityHighlightResult { Scores = scores, Explanations = explanations };
    }

    private static CredibilityResult ParseCredibilityResult(string reply)
    {
        var result = new CredibilityResult();
        var lines = reply.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("SCORE:", StringComparison.OrdinalIgnoreCase))
            {
                var val = trimmed["SCORE:".Length..].Trim();
                if (int.TryParse(val, out var s))
                    result.Score = Math.Clamp(s, 0, 100);
            }
            else if (trimmed.StartsWith("VERDICT:", StringComparison.OrdinalIgnoreCase))
            {
                result.Verdict = trimmed["VERDICT:".Length..].Trim();
            }
            else if (trimmed.StartsWith("- ") && trimmed.Contains(':'))
            {
                result.Claims.Add(trimmed[2..].Trim());
            }
        }

        return result;
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
