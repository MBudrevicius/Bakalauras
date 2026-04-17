using server.Clients;
using server.Models;

namespace server.Checks.Ai;

public class ClaudeAiModelCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.ClaudeAiModel;

    private readonly AnthropicClient _anthropic;

    public ClaudeAiModelCheck(AnthropicClient anthropic)
    {
        _anthropic = anthropic;
    }

    public async Task<AiCheckResult> RunAsync(string text, string? apiKey = null, string? model = null)
    {

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiCheckResult
            {
                Type = Type,
                Title = "Claude AI Analysis",
                Description = "No API key provided. Add your Anthropic API key in the extension settings to enable this check.",
                AiScore = 0
            };
        }

        if (text.Length < 10)
        {
            return new AiCheckResult
            {
                Type = Type,
                Title = "Claude AI Analysis",
                Description = "Text too short for reliable AI model analysis.",
                AiScore = 0
            };
        }

        var sample = text.Length > 4000 ? text[..4000] : text;

        try
        {
            var score = await _anthropic.DetectAiTextAsync(apiKey, sample, model);

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
}
