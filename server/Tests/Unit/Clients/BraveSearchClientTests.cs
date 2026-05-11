using Microsoft.Extensions.Configuration;
using server.Clients;
using server.Models;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Clients;

public class BraveSearchClientTests
{
    private BraveSearchClient CreateClient(MockHttpMessageHandler handler, string? apiKey = "test-brave-key")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BraveSearch:ApiKey"] = apiKey
            })
            .Build();
        var factory = new MockHttpClientFactory(handler);
        return new BraveSearchClient(config, factory);
    }

    [Fact]
    public async Task SearchAsync_NoApiKey_ReturnsEmpty()
    {
        var client = CreateClient(new MockHttpMessageHandler(), apiKey: null);
        var results = await client.SearchAsync("test query");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_EmptyApiKey_ReturnsEmpty()
    {
        var client = CreateClient(new MockHttpMessageHandler(), apiKey: "");
        var results = await client.SearchAsync("test query");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ApiError_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler("", System.Net.HttpStatusCode.InternalServerError);
        var client = CreateClient(handler);
        var results = await client.SearchAsync("test query");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_EmptyResponse_ReturnsEmpty()
    {
        var client = CreateClient(new MockHttpMessageHandler("{}"));
        var results = await client.SearchAsync("test query");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_NoWebResults_ReturnsEmpty()
    {
        var json = """{"web": {}}""";
        var client = CreateClient(new MockHttpMessageHandler(json));
        var results = await client.SearchAsync("test query");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ValidResults_ParsesCorrectly()
    {
        var json = """
        {
            "web": {
                "results": [
                    {"url": "https://example.com/page1", "title": "Result 1", "description": "Description 1"},
                    {"url": "https://example.com/page2", "title": "Result 2", "description": "Description 2"}
                ]
            }
        }
        """;
        var client = CreateClient(new MockHttpMessageHandler(json));
        var results = await client.SearchAsync("test query");

        Assert.Equal(2, results.Count);
        Assert.Equal("https://example.com/page1", results[0].Url);
        Assert.Equal("Result 1", results[0].Title);
        Assert.Equal("Description 1", results[0].Snippet);
    }

    [Fact]
    public async Task SearchAsync_EmptyUrl_SkipsEntry()
    {
        var json = """
        {
            "web": {
                "results": [
                    {"url": "", "title": "No URL", "description": "Skipped"},
                    {"url": "https://valid.com", "title": "Valid", "description": "Kept"}
                ]
            }
        }
        """;
        var client = CreateClient(new MockHttpMessageHandler(json));
        var results = await client.SearchAsync("test query");

        Assert.Single(results);
        Assert.Equal("https://valid.com", results[0].Url);
    }

    [Fact]
    public async Task SearchAsync_HtmlEncodedTitles_Decoded()
    {
        var json = """
        {
            "web": {
                "results": [
                    {"url": "https://example.com", "title": "Title &amp; More", "description": "Desc &lt;tag&gt;"}
                ]
            }
        }
        """;
        var client = CreateClient(new MockHttpMessageHandler(json));
        var results = await client.SearchAsync("test query");

        Assert.Single(results);
        Assert.Equal("Title & More", results[0].Title);
        Assert.Equal("Desc <tag>", results[0].Snippet);
    }

    [Fact]
    public async Task SearchAsync_MissingFields_DefaultsToEmpty()
    {
        var json = """
        {
            "web": {
                "results": [
                    {"url": "https://example.com"}
                ]
            }
        }
        """;
        var client = CreateClient(new MockHttpMessageHandler(json));
        var results = await client.SearchAsync("test query");

        Assert.Single(results);
        Assert.Equal("", results[0].Title);
        Assert.Equal("", results[0].Snippet);
    }

    [Fact]
    public async Task SearchAsync_SetsCorrectHeaders()
    {
        var handler = new MockHttpMessageHandler("""{"web":{"results":[]}}""");
        var client = CreateClient(handler);
        await client.SearchAsync("climate change");

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("X-Subscription-Token", handler.LastRequest!.Headers.Select(h => h.Key));
    }
}
