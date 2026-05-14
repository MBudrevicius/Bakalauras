using System.Net;
using System.Net.Http.Json;
using server.Models;

namespace server.Tests.Integration;

/// <summary>
/// Integration tests with varied text samples designed to exercise deeper branches
/// in AI heuristic checks (hedging, repetition, transitional phrases, paragraph structure, uniformity).
/// Also covers the all-models endpoint and highlight with Claude blending.
/// </summary>
public class AiHeuristicCoverageTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public AiHeuristicCoverageTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Text with heavy hedging language: modal verbs, concessions, qualifiers, conditional framing.
    /// Triggers high-coverage branches in HedgingLanguageCheck.
    /// </summary>
    [Fact]
    public async Task AiChecks_HeavyHedgingText_TriggersHedgingBranches()
    {
        var text = "This might be a relatively important finding, although it could be somewhat limited in scope. " +
                   "Furthermore, one should consider that these results may not necessarily apply to all contexts. " +
                   "It is arguably the case that, while the evidence suggests a trend, it could be generally overstated. " +
                   "Nevertheless, researchers would typically agree that the findings are potentially significant. " +
                   "However, it is perhaps worth noting that further investigation might reveal additional nuances. " +
                   "Consequently, one could say that the conclusions are somewhat preliminary in nature. " +
                   "Although the data appears relatively consistent, there may be underlying factors that could affect interpretation. " +
                   "If these conditions hold, the implications would presumably extend beyond the current scope. " +
                   "Whether or not this is the case, the evidence seemingly points to a fairly consistent pattern. " +
                   "Depending on how one interprets the data, the results might suggest a possibly important relationship.";

        var response = await _client.PostAsJsonAsync("/api/ai-checks", new AiCheckRequest { Text = text });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);

        var hedging = result.Results.FirstOrDefault(r => r.Type == AiCheckType.HedgingLanguage);
        Assert.NotNull(hedging);
        Assert.True(hedging.AiScore > 0, "Hedging check should produce non-zero score for heavily hedged text");
    }

    /// <summary>
    /// Text with highly repetitive phrasing: repeated sentence starters, repeated trigrams.
    /// Triggers high-coverage branches in RepetitivePhrasingCheck.
    /// </summary>
    [Fact]
    public async Task AiChecks_RepetitiveText_TriggersRepetitionBranches()
    {
        var text = "The system provides a comprehensive framework for data analysis. " +
                   "The system enables users to process large volumes of information. " +
                   "The system allows for seamless integration with existing workflows. " +
                   "The system offers multiple configuration options for customization. " +
                   "The system ensures reliable performance under heavy workloads. " +
                   "The system supports various data formats for maximum compatibility. " +
                   "The system includes built-in security features for data protection. " +
                   "The system delivers consistent results across different environments. " +
                   "The system provides real-time monitoring and alerting capabilities. " +
                   "The system facilitates collaboration between team members effectively.";

        var response = await _client.PostAsJsonAsync("/api/ai-checks", new AiCheckRequest { Text = text });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);

        var repetitive = result.Results.FirstOrDefault(r => r.Type == AiCheckType.RepetitivePhrasing);
        Assert.NotNull(repetitive);
        Assert.True(repetitive.AiScore > 0, $"Repetition score should be non-zero for repetitive text, got {repetitive.AiScore}");
    }

    /// <summary>
    /// Text with heavy transitional phrases: conjunctive adverbs, "it is important to", demonstrative starters.
    /// Triggers high-coverage branches in TransitionalPhraseCheck.
    /// </summary>
    [Fact]
    public async Task AiChecks_TransitionalHeavyText_TriggersTransitionalBranches()
    {
        var text = "Furthermore, the research demonstrates a clear correlation between variables. " +
                   "Moreover, additional studies have confirmed these initial findings. " +
                   "It is important to note that the sample size was sufficiently large. " +
                   "This suggests that the methodology was sound and replicable. " +
                   "Consequently, the implications extend beyond the original hypothesis. " +
                   "Additionally, it is worth mentioning that control groups showed no effect. " +
                   "Nevertheless, certain limitations should be acknowledged. " +
                   "That demonstrates the robustness of the experimental design. " +
                   "In conclusion, the evidence strongly supports the central claim. " +
                   "Subsequently, future research should explore these directions further. " +
                   "This indicates a paradigm shift in how we understand the phenomenon. " +
                   "It is essential to recognize the broader context of these results.";

        var response = await _client.PostAsJsonAsync("/api/ai-checks", new AiCheckRequest { Text = text });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);

        var transitional = result.Results.FirstOrDefault(r => r.Type == AiCheckType.TransitionalPhrases);
        Assert.NotNull(transitional);
        Assert.True(transitional.AiScore >= 40, $"Transitional score should be high, got {transitional.AiScore}");
    }

    /// <summary>
    /// Multi-paragraph text with uniform structure to trigger ParagraphStructureCheck.
    /// </summary>
    [Fact]
    public async Task AiChecks_UniformParagraphs_TriggersParagraphStructureBranches()
    {
        var text = "The first major advancement in artificial intelligence came with the development of machine learning algorithms. " +
                   "These algorithms could process large datasets and identify patterns that humans might miss. " +
                   "Researchers quickly recognized the potential applications across multiple industries.\n\n" +
                   "The second major breakthrough was the introduction of deep learning techniques. " +
                   "These neural networks could handle more complex tasks with greater accuracy. " +
                   "Scientists were amazed by the speed of improvement in benchmark performance.\n\n" +
                   "The third significant development involved natural language processing models. " +
                   "These transformer architectures could understand and generate human text effectively. " +
                   "Companies immediately saw the commercial potential of these capabilities.\n\n" +
                   "The fourth key innovation was the scaling of large language models dramatically. " +
                   "These foundation models demonstrated emergent abilities at unprecedented scale. " +
                   "Researchers debated the implications for artificial general intelligence timelines.\n\n" +
                   "The fifth notable progress area was multimodal AI combining vision and language. " +
                   "These systems could process images and text simultaneously for richer understanding. " +
                   "Developers created applications that leveraged both modalities for better results.";

        var response = await _client.PostAsJsonAsync("/api/ai-checks", new AiCheckRequest { Text = text });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);

        var paragraph = result.Results.FirstOrDefault(r => r.Type == AiCheckType.ParagraphStructure);
        Assert.NotNull(paragraph);
        Assert.True(paragraph.AiScore > 0, "Paragraph structure check should find uniform paragraphs");
    }

    /// <summary>
    /// Text with very uniform sentence lengths to trigger SentenceUniformityCheck deep branches.
    /// </summary>
    [Fact]
    public async Task AiChecks_UniformSentenceLengths_TriggersUniformityBranches()
    {
        var text = "The quick brown fox jumped gracefully over the lazy sleeping dog today. " +
                   "Modern technology has transformed the way people communicate with each other daily. " +
                   "Scientists discovered a new species of deep sea creatures last week unexpectedly. " +
                   "The government announced new economic policies that will affect all small businesses soon. " +
                   "Researchers published their findings in a peer reviewed journal this past month. " +
                   "The company reported record quarterly profits due to strong consumer demand growth. " +
                   "Environmental activists organized a large protest against the proposed construction project today. " +
                   "Students performed exceptionally well on their final examinations across all subject areas.";

        var response = await _client.PostAsJsonAsync("/api/ai-checks", new AiCheckRequest { Text = text });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);

        var uniformity = result.Results.FirstOrDefault(r => r.Type == AiCheckType.SentenceUniformity);
        Assert.NotNull(uniformity);
        Assert.True(uniformity.AiScore >= 40, $"Uniformity score should be elevated for uniform sentences, got {uniformity.AiScore}");
    }

    /// <summary>
    /// Highly varied human-like text to exercise the low-score branches of heuristics.
    /// </summary>
    [Fact]
    public async Task AiChecks_NaturalHumanText_TriggersLowScoreBranches()
    {
        var text = "I went to the store yesterday. " +
                   "Bought some milk, bread, eggs—the usual stuff. " +
                   "Oh, and I ran into Dave! Haven't seen him in ages. " +
                   "He's doing well, apparently got a new job at some tech startup downtown. " +
                   "Anyway. " +
                   "So the thing is, I've been thinking about switching careers myself. " +
                   "Not sure if it's the right move though... like, what if I hate it? " +
                   "My sister says I overthink everything (she's probably right lol). " +
                   "The weather was gorgeous yesterday at least—finally feels like spring! " +
                   "Made me want to just skip work and go for a hike or something.";

        var response = await _client.PostAsJsonAsync("/api/ai-checks", new AiCheckRequest { Text = text });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);
        Assert.True(result.OverallAiScore < 60, $"Natural text should score low, got {result.OverallAiScore}");
    }

    /// <summary>
    /// Tests the all-models endpoint which runs Claude on 3 models in parallel.
    /// Covers AiCheckService.RunAllModelsAsync and AllModelsAiCheckResponse/ModelResult models.
    /// </summary>
    [Fact]
    public async Task AiChecksAllModels_WithApiKey_ReturnsThreeModelResults()
    {
        _factory.FakeApiHandler.AnthropicAiScore = 72;

        var text = "The implementation of machine learning algorithms has fundamentally transformed how organizations process and analyze data. " +
                   "Furthermore, deep learning architectures enable unprecedented accuracy in pattern recognition tasks. " +
                   "Additionally, natural language processing has made significant strides in understanding human communication. " +
                   "It is important to note that these advancements could potentially benefit a wide range of industries. " +
                   "Consequently, businesses should consider integrating these technologies into their workflows effectively.";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai-checks/all-models");
        request.Headers.Add("X-Claude-Api-Key", "test-key-all-models");
        request.Content = JsonContent.Create(new AiCheckRequest { Text = text });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AllModelsAiCheckResponse>();
        Assert.NotNull(result);
        Assert.True(result.TextLength > 0);
        Assert.Equal(3, result.ModelResults.Count);
        Assert.NotEmpty(result.HeuristicResults);
        Assert.InRange(result.AverageAiScore, 0, 100);

        foreach (var mr in result.ModelResults)
        {
            Assert.NotEmpty(mr.Model);
            Assert.NotEmpty(mr.Label);
            Assert.InRange(mr.AiScore, 0, 100);
            Assert.InRange(mr.OverallAiScore, 0, 100);
        }
    }

    /// <summary>
    /// Tests highlight endpoint with API key which triggers the Claude blending path in AnalyzeSegmentsAsync.
    /// </summary>
    [Fact]
    public async Task Highlight_WithApiKey_BlendsClaudeAndHeuristics()
    {
        _factory.FakeApiHandler.AnthropicAiScore = 70;

        var segments = new[]
        {
            "The implementation of sophisticated algorithms has transformed modern computing paradigms significantly.",
            "I grabbed coffee and sat by the window watching people walk by in the rain.",
            "Furthermore, these systems demonstrate unprecedented levels of accuracy and reliability in production environments.",
            "yeah idk man it just kinda works you know what I mean lol",
            "The comprehensive framework enables seamless integration with existing enterprise infrastructure components."
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai-checks/highlight");
        request.Headers.Add("X-Claude-Api-Key", "test-key-highlight");
        request.Headers.Add("X-Claude-Model", "claude-haiku-4-5-20251001");
        request.Content = JsonContent.Create(new HighlightRequest { Segments = segments });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("scores", content);
    }

    /// <summary>
    /// Tests highlight with segments shorter than 5 words (triggers early return branch).
    /// </summary>
    [Fact]
    public async Task Highlight_ShortSegments_ReturnsZeroScores()
    {
        var segments = new[]
        {
            "Hi there",
            "OK",
            "The comprehensive implementation of machine learning algorithms demonstrates remarkable efficiency.",
            "",
            "Test"
        };

        var response = await _client.PostAsJsonAsync("/api/ai-checks/highlight",
            new HighlightRequest { Segments = segments });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Empty text with URL triggers HtmlTextExtractor path.
    /// </summary>
    [Fact]
    public async Task AiChecks_WithUrlOnly_ExtractsTextFromFakeResponse()
    {
        var response = await _client.PostAsJsonAsync("/api/ai-checks",
            new AiCheckRequest { Text = "", Url = "https://text-extract-test.com/article" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);
        Assert.True(result.TextLength > 0);
    }

    /// <summary>
    /// All-models with URL-only request (covers text extraction in RunAllModelsAsync).
    /// </summary>
    [Fact]
    public async Task AiChecksAllModels_WithUrlOnly_ExtractsAndAnalyzes()
    {
        _factory.FakeApiHandler.AnthropicAiScore = 55;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai-checks/all-models");
        request.Headers.Add("X-Claude-Api-Key", "test-key");
        request.Content = JsonContent.Create(new AiCheckRequest { Url = "https://url-only-test.com/page" });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AllModelsAiCheckResponse>();
        Assert.NotNull(result);
        Assert.True(result.TextLength > 0);
    }

    /// <summary>
    /// Combines hedging + transitional + repetitive patterns that collectively trigger multiple
    /// high-score branches across all checks simultaneously.
    /// </summary>
    [Fact]
    public async Task AiChecks_CombinedAiPatterns_TriggersMultipleHighScorePaths()
    {
        var text = "Furthermore, it is important to note that the system could potentially provide significant benefits. " +
                   "Moreover, it is essential to recognize that these advancements might fundamentally change the landscape. " +
                   "Additionally, it is worth mentioning that the evidence suggests a somewhat consistent pattern. " +
                   "Consequently, this demonstrates that the methodology may yield relatively reliable results overall. " +
                   "Nevertheless, this indicates that further research could potentially reveal additional insights. " +
                   "Similarly, this suggests that the framework would generally apply across various contexts. " +
                   "Likewise, this implies that the findings might be somewhat generalizable to other domains. " +
                   "Subsequently, this shows that the approach could conceivably scale to larger datasets too. " +
                   "Accordingly, this highlights that the technique may offer arguably superior performance. " +
                   "Therefore, this means that the conclusions should be interpreted with appropriate caution here.";

        var response = await _client.PostAsJsonAsync("/api/ai-checks", new AiCheckRequest { Text = text });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);
        Assert.True(result.OverallAiScore >= 30, $"Combined AI patterns should score meaningfully, got {result.OverallAiScore}");
    }
}
