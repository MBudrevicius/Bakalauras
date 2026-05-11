using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Data;
using server.Models;

namespace server.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Remove ALL EF registrations (both DbContextOptions and the DbContext itself)
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true ||
                d.ImplementationType?.FullName?.Contains("Npgsql") == true)
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            // Re-add with InMemory
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));
        });
    }
}

public class EndpointIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EndpointIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // --- Security Endpoints ---

    [Fact]
    public async Task SecurityChecks_EmptyUrl_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/security-checks", new SecurityCheckRequest { Url = "" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SecurityChecks_InvalidUrl_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/security-checks", new SecurityCheckRequest { Url = "not-a-url" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SecurityChecks_ValidUrl_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/security-checks", new SecurityCheckRequest { Url = "https://example.com" });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // --- AI Endpoints ---

    [Fact]
    public async Task AiChecks_EmptyTextAndUrl_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/ai-checks", new AiCheckRequest { Text = "", Url = "" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AiChecks_WithText_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/ai-checks", new AiCheckRequest { Text = "Some text to analyze for AI" });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AiChecksAllModels_NoApiKey_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/ai-checks/all-models", new AiCheckRequest { Text = "Some text" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AiChecksAllModels_EmptyText_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/ai-checks/all-models", new AiCheckRequest { Text = "" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Highlight_EmptySegments_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/ai-checks/highlight", new HighlightRequest { Segments = [] });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Highlight_TooManySegments_ReturnsBadRequest()
    {
        var segments = Enumerable.Range(0, 501).Select(i => $"segment {i}").ToArray();
        var response = await _client.PostAsJsonAsync("/api/ai-checks/highlight", new HighlightRequest { Segments = segments });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Highlight_ValidSegments_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/ai-checks/highlight",
            new HighlightRequest { Segments = ["This is a test paragraph with enough words"] });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // --- Info Endpoints ---

    [Fact]
    public async Task CrossCheck_EmptyUrl_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/cross-check",
            new CrossCheckRequest { Url = "" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CrossCheck_InvalidUrl_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/cross-check",
            new CrossCheckRequest { Url = "not-valid" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CrossCheck_ValidUrl_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/cross-check",
            new CrossCheckRequest { Url = "https://example.com", Title = "Test", Text = "Content" });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CrossCheckAllModels_NoApiKey_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/cross-check/all-models",
            new CrossCheckRequest { Url = "https://example.com", Title = "Test", Text = "Content" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CrossCheckAllModels_EmptyUrl_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/cross-check/all-models",
            new CrossCheckRequest { Url = "" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PageScore_EmptyUrl_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/page-score?url=");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PageScore_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/page-score?url=https://nonexistent.example.com");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("false", content);
    }
}
