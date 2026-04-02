using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using server.Models;

namespace server.Services;

/// <summary>
/// Sends text to Claude AI model for a second-opinion AI detection check.
/// Requires Anthropic API key in configuration.
/// </summary>
public class ClaudeAiModelCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.ClaudeAiModel;

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    private const string AnthropicEndpoint = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-3-5-haiku-20241022";
    private const int MaxTextLength = 4000; // limit to keep tokens/cost manageable

    public ClaudeAiModelCheck(IConfiguration config, IHttpClientFactory httpFactory)
    {
        _config = config;
        _httpFactory = httpFactory;
    }

    public async Task<AiCheckResult> RunAsync(string text)
    {
        var apiKey = _config["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiCheckResult
            {
                Type = Type,
                Title = "Claude AI Analysis",
                Description = "API key not configured. Skipping check.",
                AiScore = 0
            };
        }

        if (text.Length < 100)
        {
            return new AiCheckResult
            {
                Type = Type,
                Title = "Claude AI Analysis",
                Description = "Text too short for reliable AI model analysis.",
                AiScore = 0
            };
        }

        // Truncate to keep cost/latency down
        var sample = text.Length > MaxTextLength
            ? text[..MaxTextLength]
            : text;

        try
        {
            var score = await CallClaudeAsync(apiKey, sample);
            var label = score switch
            {
                >= 80 => "very likely AI-generated",
                >= 60 => "likely AI-generated",
                >= 40 => "unclear origin",
                >= 20 => "likely human-written",
                _     => "very likely human-written"
            };

            return new AiCheckResult
            {
                Type = Type,
                Title = "Claude AI Analysis",
                Description = $"Claude judges this text as {label} ({score}% AI probability).",
                AiScore = score
            };
        }
        catch (Exception ex)
        {
            return new AiCheckResult
            {
                Type = Type,
                Title = "Claude AI Analysis",
                Description = $"API call failed: {ex.Message}",
                AiScore = 0
            };
        }
    }

    private async Task<int> CallClaudeAsync(string apiKey, string sample)
    {
        var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var requestBody = new
        {
            model = Model,
            max_tokens = 60,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = $"""
                        You are an AI-generated text detector. Analyze the following text and estimate the probability (0-100) that it was written by an AI language model rather than a human.

                        Reply with ONLY a single integer between 0 and 100, nothing else. 0 means certainly human, 100 means certainly AI.

                        Text to analyze:
                        ---
                        {sample}
                        ---
                        """
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(AnthropicEndpoint, content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic API returned {(int)response.StatusCode}");

        // Parse the response to extract the text content
        using var doc = JsonDocument.Parse(responseJson);
        var textBlock = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "";

        // Extract the integer score from Claude's reply
        var cleaned = textBlock.Trim();
        if (int.TryParse(cleaned, out var score))
            return Math.Clamp(score, 0, 100);

        // If Claude returned text around the number, try to find it
        foreach (var word in cleaned.Split(' ', '\n', '\r', '\t', '.', ',', '%'))
        {
            if (int.TryParse(word.Trim(), out var parsed))
                return Math.Clamp(parsed, 0, 100);
        }

        throw new InvalidOperationException($"Could not parse score from Claude response: {cleaned}");
    }
}
