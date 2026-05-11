using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using server.Clients;
using server.Data;
using server.Helpers;
using server.Models;
using server.Services;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Services;

public class CrossCheckServiceAsyncTests
{
    private static string AnthropicJsonResponse(string text) =>
        JsonSerializer.Serialize(new { content = new[] { new { text } } });

    private static string BraveSearchJsonResponse(params (string url, string title, string snippet)[] results)
    {
        var items = results.Select(r => new { url = r.url, title = r.title, description = r.snippet });
        return JsonSerializer.Serialize(new { web = new { results = items } });
    }

    /// <summary>
    /// Handler that returns different responses based on request URL
    /// </summary>
    private class MultiResponseHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public MultiResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    private static (CrossCheckService service, AppDbContext db) CreateService(HttpMessageHandler handler)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var factory = new MockHttpClientFactory(handler);
        var htmlExtractor = new HtmlTextExtractor(factory);
        var scoreStore = new PageScoreStore(db, htmlExtractor);
        var anthropic = new AnthropicClient(factory);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "BraveSearch:ApiKey", "test-key" } })
            .Build();
        var braveSearch = new BraveSearchClient(config, factory);
        return (new CrossCheckService(braveSearch, anthropic, scoreStore), db);
    }

    [Fact]
    public async Task CrossCheckAsync_NoApiKey_UsesCleanTitle()
    {
        var braveResponse = BraveSearchJsonResponse(
            ("https://source.com/article", "Related Article", "Some relevant snippet"));
        var handler = new MockHttpMessageHandler(braveResponse);

        var (service, db) = CreateService(handler);

        var response = await service.CrossCheckAsync(
            "https://example.com", "Article Title - SiteName", "Some text",
            [], claudeApiKey: null);

        // Without API key, should use CleanTitle instead of ExtractTopic
        Assert.Equal("Article Title", response.Topic);
        Assert.Equal("https://example.com", response.Url);
        db.Dispose();
    }

    [Fact]
    public async Task CrossCheckAsync_WithApiKey_UsesExtractedTopic()
    {
        var callCount = 0;
        var handler = new MultiResponseHandler(request =>
        {
            if (request.RequestUri!.Host == "api.anthropic.com")
            {
                callCount++;
                // First call = ExtractTopic, second = EvaluateSourceReliability, third = VerifyCredibility
                if (callCount == 1)
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(AnthropicJsonResponse("\"climate change 2026\"")) };
                if (callCount == 2)
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(AnthropicJsonResponse("[0] 85")) };
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(AnthropicJsonResponse("SCORE: 80\nVERDICT: Mostly Supported\nCLAIMS:\n- Claim: Supported - reason")) };
            }
            // Brave search response
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BraveSearchJsonResponse(
                    ("https://source.com/article", "Source Article", "snippet text")))
            };
        });

        var (service, db) = CreateService(handler);

        var response = await service.CrossCheckAsync(
            "https://example.com", "Title", "Some article text for analysis",
            [], claudeApiKey: "sk-test");

        Assert.Equal("climate change 2026", response.Topic);
        Assert.NotNull(response.Credibility);
        Assert.Equal(80, response.Credibility!.Score);
        db.Dispose();
    }

    [Fact]
    public async Task CrossCheckAsync_EmptyText_StillWorks()
    {
        var braveResponse = BraveSearchJsonResponse(
            ("https://source.com", "Source", "snippet"));
        var handler = new MockHttpMessageHandler(braveResponse);
        var (service, db) = CreateService(handler);

        var response = await service.CrossCheckAsync(
            "https://example.com", "Title", "",
            [], claudeApiKey: "sk-test");

        Assert.Null(response.Credibility);
        db.Dispose();
    }

    [Fact]
    public async Task CrossCheckAsync_WithPageLinks_AnalyzesThem()
    {
        var handler = new MockHttpMessageHandler(BraveSearchJsonResponse());
        var (service, db) = CreateService(handler);

        var pageLinks = new List<string>
        {
            "https://external1.com/page",
            "https://external2.com/page",
            "https://example.com/internal"  // same domain, should be excluded
        };

        var response = await service.CrossCheckAsync(
            "https://example.com", "Title", "text",
            pageLinks);

        Assert.Equal(2, response.PageLinkDomains);
        Assert.Equal(2, response.PageLinkSamples.Count);
        db.Dispose();
    }

    [Fact]
    public async Task CrossCheckAsync_FiltersSameDomainFromSearchResults()
    {
        var braveResponse = BraveSearchJsonResponse(
            ("https://example.com/other-page", "Same Domain", "snippet"),
            ("https://different.com/article", "Different Source", "relevant snippet"));
        var handler = new MockHttpMessageHandler(braveResponse);
        var (service, db) = CreateService(handler);

        var response = await service.CrossCheckAsync(
            "https://example.com/page", "Title", "",
            []);

        // Should filter out same-domain results
        Assert.DoesNotContain(response.RelatedPages, p => p.Url.Contains("example.com"));
        db.Dispose();
    }

    [Fact]
    public async Task CrossCheckAsync_SavesCredibilityScore()
    {
        var callCount = 0;
        var handler = new MultiResponseHandler(request =>
        {
            if (request.RequestUri!.Host == "api.anthropic.com")
            {
                callCount++;
                if (callCount == 1) // ExtractTopic
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(AnthropicJsonResponse("\"test topic\"")) };
                if (callCount == 2) // EvaluateSourceReliability
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(AnthropicJsonResponse("[0] 90")) };
                // VerifyCredibility
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(AnthropicJsonResponse("SCORE: 75\nVERDICT: Supported")) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BraveSearchJsonResponse(
                    ("https://source.com", "Source", "snippet")))
            };
        });

        var (service, db) = CreateService(handler);

        await service.CrossCheckAsync(
            "https://testsite.com", "Title", "Article text for analysis",
            [], claudeApiKey: "sk-test");

        var pageScore = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "testsite.com");
        Assert.NotNull(pageScore);
        Assert.Equal(75, pageScore!.CredibilityScore);
        db.Dispose();
    }

    [Fact]
    public async Task CrossCheckAsync_NoCredibility_DoesNotSave()
    {
        var handler = new MockHttpMessageHandler(BraveSearchJsonResponse());
        var (service, db) = CreateService(handler);

        await service.CrossCheckAsync(
            "https://testsite.com", "Title", "",
            []);

        Assert.Empty(db.PageScores);
        db.Dispose();
    }

    // CrossCheckAllModelsAsync tests

    [Fact]
    public async Task CrossCheckAllModelsAsync_EmptyTopic_UsesCleanTitle()
    {
        var callCount = 0;
        var handler = new MultiResponseHandler(request =>
        {
            if (request.RequestUri!.Host == "api.anthropic.com")
            {
                callCount++;
                if (callCount == 1) // ExtractTopic returns whitespace
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(AnthropicJsonResponse("   ")) };
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(AnthropicJsonResponse("50")) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(BraveSearchJsonResponse()) };
        });

        var (service, db) = CreateService(handler);

        var response = await service.CrossCheckAllModelsAsync(
            "https://example.com", "Breaking News Title | SiteName", "text", [], "sk-test");

        Assert.Equal("Breaking News Title", response.Topic);
        db.Dispose();
    }

    [Fact]
    public async Task CrossCheckAllModelsAsync_WithSources_RunsAllModels()
    {
        var handler = new MultiResponseHandler(request =>
        {
            if (request.RequestUri!.Host == "api.anthropic.com")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(AnthropicJsonResponse("SCORE: 70\nVERDICT: Mostly Supported")) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BraveSearchJsonResponse(
                    ("https://src.com", "Source", "snippet")))
            };
        });

        var (service, db) = CreateService(handler);

        var response = await service.CrossCheckAllModelsAsync(
            "https://example.com", "Title", "Article text",
            [], "sk-test");

        // Should have model results (3 models)
        Assert.NotNull(response.ModelResults);
        Assert.NotNull(response.Credibility);
        db.Dispose();
    }

    [Fact]
    public async Task CrossCheckAllModelsAsync_NoRelatedPages_NoCredibility()
    {
        var callCount = 0;
        var handler = new MultiResponseHandler(request =>
        {
            if (request.RequestUri!.Host == "api.anthropic.com")
            {
                callCount++;
                if (callCount == 1) // ExtractTopic
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(AnthropicJsonResponse("\"niche topic\"")) };
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(AnthropicJsonResponse("50")) };
            }
            // Brave returns no results
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new { web = new { results = Array.Empty<object>() } })) };
        });

        var (service, db) = CreateService(handler);

        var response = await service.CrossCheckAllModelsAsync(
            "https://example.com", "Title", "text", [], "sk-test");

        Assert.Null(response.Credibility);
        Assert.Empty(response.RelatedPages);
        db.Dispose();
    }
}
