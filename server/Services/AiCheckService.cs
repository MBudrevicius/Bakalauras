using server.Checks.Ai;
using server.Clients;
using server.Helpers;
using server.Models;

namespace server.Services;

public class AiCheckService
{
    private readonly IEnumerable<IAiCheck> _checks;
    private readonly PageScoreStore _scoreStore;
    private readonly AnthropicClient _anthropic;
    private readonly HtmlTextExtractor _htmlExtractor;

    public AiCheckService(IEnumerable<IAiCheck> checks, PageScoreStore scoreStore, AnthropicClient anthropic, HtmlTextExtractor htmlExtractor)
    {
        _checks = checks;
        _scoreStore = scoreStore;
        _anthropic = anthropic;
        _htmlExtractor = htmlExtractor;
    }

    public async Task<AiCheckResponse> RunAllAsync(AiCheckRequest request, string? claudeApiKey = null, string? claudeModel = null)
    {
        var text = request.Text;
        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(request.Url))
        {
            text = await _htmlExtractor.ExtractTextFromUrl(request.Url);
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

        var tasks = _checks.Select(c => c.RunAsync(text, claudeApiKey, claudeModel));
        var results = await Task.WhenAll(tasks);

        var response = new AiCheckResponse
        {
            AnalyzedText = text.Length > 500 ? text[..500] + "\u2026" : text,
            TextLength = text.Length,
            Results = [.. results.OrderByDescending(r => r.Type == AiCheckType.ClaudeAiModel)],
            OverallAiScore = CalculateOverallScore(results)
        };

        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            await _scoreStore.SavePageScoreAsync(request.Url, securityScore: null, credibilityScore: null, aiScore: response.OverallAiScore);
        }

        return response;
    }

    private static int CalculateOverallScore(AiCheckResult[] results)
    {
        if (results.Length == 0)
        {
            return 0;
        }

        var claude = results.FirstOrDefault(r => r.Type == AiCheckType.ClaudeAiModel && r.AiScore > 0);
        var others = results.Where(r => r.Type != AiCheckType.ClaudeAiModel && r.AiScore > 0).ToList();

        var checkWeights = new Dictionary<AiCheckType, double>
        {
            { AiCheckType.SentenceUniformity, 1.2 },
            { AiCheckType.RepetitivePhrasing, 1.2 },
            { AiCheckType.PerplexityEstimation, 1.1 },
            { AiCheckType.VocabularyRichness, 1.0 },
            { AiCheckType.TransitionalPhrases, 1.0 },
            { AiCheckType.ParagraphStructure, 0.9 },
            { AiCheckType.PunctuationPatterns, 0.8 },
            { AiCheckType.HedgingLanguage, 0.7 },
        };

        var weightedSum = 0.0;
        var totalWeight = 0.0;
        foreach (var r in others)
        {
            var weight = checkWeights.GetValueOrDefault(r.Type, 1.0);
            weightedSum += r.AiScore * weight;
            totalWeight += weight;
        }

        var othersWeighted = totalWeight > 0 ? weightedSum / totalWeight : 0;

        if (claude != null)
        {
            return (int)Math.Round(claude.AiScore * 0.6 + othersWeighted * 0.4);
        }

        return (int)Math.Round(othersWeighted);
    }

    public async Task<AllModelsAiCheckResponse> RunAllModelsAsync(AiCheckRequest request, string claudeApiKey)
    {
        var text = request.Text;
        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(request.Url))
        {
            text = await _htmlExtractor.ExtractTextFromUrl(request.Url);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new AllModelsAiCheckResponse { TextLength = 0 };
        }

        var heuristicChecks = _checks.Where(c => c.Type != AiCheckType.ClaudeAiModel);
        var heuristicResults = await Task.WhenAll(heuristicChecks.Select(c => c.RunAsync(text)));

        var models = new[]
        {
            ("claude-haiku-4-5-20251001", "Haiku 4.5"),
            ("claude-sonnet-4-6", "Sonnet 4.6"),
            ("claude-opus-4-7", "Opus 4.7")
        };

        var modelTasks = models.Select(async m =>
        {
            var score = await _anthropic.DetectAiTextAsync(claudeApiKey, text, m.Item1);
            return new ModelResult
            {
                Model = m.Item1,
                Label = m.Item2,
                AiScore = score,
                OverallAiScore = CalculateOverallScoreFromParts(score, heuristicResults)
            };
        });

        var modelResults = await Task.WhenAll(modelTasks);
        var averageScore = (int)Math.Round(modelResults.Average(r => r.OverallAiScore));

        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            await _scoreStore.SavePageScoreAsync(request.Url, securityScore: null, credibilityScore: null, aiScore: averageScore);
        }

        return new AllModelsAiCheckResponse
        {
            AverageAiScore = averageScore,
            TextLength = text.Length,
            ModelResults = [.. modelResults],
            HeuristicResults = [.. heuristicResults.OrderByDescending(r => r.AiScore)]
        };
    }

    private static int CalculateOverallScoreFromParts(int claudeScore, AiCheckResult[] heuristicResults)
    {
        var checkWeights = new Dictionary<AiCheckType, double>
        {
            { AiCheckType.SentenceUniformity, 1.2 },
            { AiCheckType.RepetitivePhrasing, 1.2 },
            { AiCheckType.PerplexityEstimation, 1.1 },
            { AiCheckType.VocabularyRichness, 1.0 },
            { AiCheckType.TransitionalPhrases, 1.0 },
            { AiCheckType.ParagraphStructure, 0.9 },
            { AiCheckType.PunctuationPatterns, 0.8 },
            { AiCheckType.HedgingLanguage, 0.7 },
        };

        var weightedSum = 0.0;
        var totalWeight = 0.0;
        foreach (var r in heuristicResults.Where(r => r.AiScore > 0))
        {
            var weight = checkWeights.GetValueOrDefault(r.Type, 1.0);
            weightedSum += r.AiScore * weight;
            totalWeight += weight;
        }

        var othersWeighted = totalWeight > 0 ? weightedSum / totalWeight : 0;

        if (claudeScore > 0)
        {
            return (int)Math.Round(claudeScore * 0.6 + othersWeighted * 0.4);
        }

        return (int)Math.Round(othersWeighted);
    }

    public async Task<int[]> AnalyzeSegmentsAsync(string[] segments, string? claudeApiKey = null, string? claudeModel = null)
    {
        var paragraphChecks = _checks
            .Where(c => c.Type != AiCheckType.ClaudeAiModel)
            .ToList();

        var heuristicTasks = segments.Select(async segment =>
        {
            if (string.IsNullOrWhiteSpace(segment) || segment.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 5)
            {
                return 0;
            }

            var results = await Task.WhenAll(paragraphChecks.Select(c => c.RunAsync(segment)));
            return (int)Math.Round(results.Average(r => r.AiScore));
        });
        var heuristicScores = await Task.WhenAll(heuristicTasks);

        var claudeScores = await BatchClaudeAnalysisAsync(segments, claudeApiKey, claudeModel);
        if (claudeScores == null)
        {
            return heuristicScores;
        }

        var blended = new int[segments.Length];
        for (var i = 0; i < segments.Length; i++)
        {
            if (claudeScores[i] > 0)
            {
                blended[i] = (int)Math.Round(claudeScores[i] * 0.6 + heuristicScores[i] * 0.4);
            }
            else
            {
                blended[i] = heuristicScores[i];
            }
        }
        return blended;
    }

    private async Task<int[]?> BatchClaudeAnalysisAsync(string[] segments, string? apiKey, string? model)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || segments.Length == 0)
        {
            return null;
        }

        return await _anthropic.DetectAiSegmentsAsync(apiKey, segments, model);
    }
}
