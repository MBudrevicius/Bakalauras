using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using server.Data;

namespace server.Tests.Integration;

/// <summary>
/// Intercepts ALL outgoing HTTP requests - returns fake responses for external APIs
/// (Anthropic, BraveSearch) and generic responses for other URLs (security check targets).
/// </summary>
public class FakeExternalApiHandler : HttpMessageHandler
{
    public int AnthropicAiScore { get; set; } = 65;
    public string AnthropicTopic { get; set; } = "test topic for fact-checking";
    public int AnthropicCredibilityScore { get; set; } = 75;
    public string AnthropicCredibilityVerdict { get; set; } = "Mostly Supported";
    public List<string> AnthropicClaims { get; set; } = ["Claim 1: Supported - matches sources"];
    public int[] AnthropicSourceReliabilityScores { get; set; } = [85, 70, 60];

    public List<(string Url, string Title, string Snippet)> BraveSearchResults { get; set; } =
    [
        ("https://reliable-source.com/article", "Reliable Source Article", "Supporting evidence from authoritative source"),
        ("https://another-source.org/report", "Research Report", "Additional corroborating information from research")
    ];

    /// <summary>When true, Google Safe Browsing returns threat matches.</summary>
    public bool GoogleSafeBrowsingThreatDetected { get; set; } = false;

    /// <summary>When set, requests to this host return a redirect response.</summary>
    public string? RedirectFromHost { get; set; }
    public string? RedirectToUrl { get; set; }
    public bool RedirectDowngradeHttps { get; set; } = false;

    /// <summary>When set, returns HTML with mixed content (http:// resources on https page).</summary>
    public string? MixedContentHost { get; set; }

    /// <summary>When set, returns HTML with suspicious links for SuspiciousLinksCheck.</summary>
    public string? SuspiciousLinksHost { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";

        if (url.Contains("api.anthropic.com"))
        {
            return Task.FromResult(HandleAnthropicRequest(request));
        }

        if (url.Contains("api.search.brave.com"))
        {
            return Task.FromResult(HandleBraveSearchRequest());
        }

        if (url.Contains("safebrowsing.googleapis.com"))
        {
            return Task.FromResult(HandleGoogleSafeBrowsingRequest());
        }

        if (RedirectFromHost != null && url.Contains(RedirectFromHost))
        {
            return Task.FromResult(HandleRedirectRequest());
        }

        if (MixedContentHost != null && url.Contains(MixedContentHost))
        {
            return Task.FromResult(HandleMixedContentRequest());
        }

        if (SuspiciousLinksHost != null && url.Contains(SuspiciousLinksHost))
        {
            return Task.FromResult(HandleSuspiciousLinksRequest());
        }

        return Task.FromResult(HandleGenericRequest(request));
    }

    private HttpResponseMessage HandleAnthropicRequest(HttpRequestMessage request)
    {
        var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";

        string replyText;

        if (body.Contains("AI-generated text detector") && body.Contains("[0]"))
        {
            var lines = new StringBuilder();
            for (int i = 0; i < 20; i++)
                lines.AppendLine($"[{i}] {AnthropicAiScore}");
            replyText = lines.ToString();
        }
        else if (body.Contains("AI-generated text detector"))
        {
            replyText = AnthropicAiScore.ToString();
        }
        else if (body.Contains("precision fact-checking assistant") || body.Contains("optimal search query"))
        {
            replyText = AnthropicTopic;
        }
        else if (body.Contains("expert fact-checker") || body.Contains("misinformation analyst"))
        {
            var claims = string.Join("\n", AnthropicClaims.Select(c => $"- {c}"));
            replyText = $"SCORE: {AnthropicCredibilityScore}\nVERDICT: {AnthropicCredibilityVerdict}\nCLAIMS:\n{claims}";
        }
        else if (body.Contains("source evaluator") || body.Contains("relevant and reliable"))
        {
            var lines = new StringBuilder();
            for (int i = 0; i < AnthropicSourceReliabilityScores.Length; i++)
                lines.AppendLine($"[{i}] {AnthropicSourceReliabilityScores[i]}");
            replyText = lines.ToString();
        }
        else
        {
            replyText = "50";
        }

        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = replyText } }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
    }

    private HttpResponseMessage HandleBraveSearchRequest()
    {
        var results = BraveSearchResults.Select(r => new
        {
            url = r.Url,
            title = r.Title,
            description = r.Snippet
        }).ToArray();

        var responseJson = JsonSerializer.Serialize(new
        {
            web = new { results }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
    }

    private HttpResponseMessage HandleGoogleSafeBrowsingRequest()
    {
        if (GoogleSafeBrowsingThreatDetected)
        {
            var responseJson = JsonSerializer.Serialize(new
            {
                matches = new[]
                {
                    new { threatType = "SOCIAL_ENGINEERING", platformType = "ANY_PLATFORM" },
                    new { threatType = "MALWARE", platformType = "ANY_PLATFORM" }
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }

        var safeJson = JsonSerializer.Serialize(new { matches = Array.Empty<object>() });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(safeJson, Encoding.UTF8, "application/json")
        };
    }

    private HttpResponseMessage HandleRedirectRequest()
    {
        var target = RedirectToUrl ?? "https://example.com/final";
        if (RedirectDowngradeHttps)
        {
            target = target.Replace("https://", "http://");
        }

        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        response.Headers.Location = new Uri(target);
        return response;
    }

    private static HttpResponseMessage HandleMixedContentRequest()
    {
        var html = """
            <html>
            <head><title>Mixed Content Page</title></head>
            <body>
            <script src="http://insecure-cdn.com/script.js"></script>
            <iframe src="http://insecure-embed.com/widget"></iframe>
            <img src="http://insecure-images.com/photo.jpg" />
            <img src="http://insecure-images.com/banner.png" />
            <audio src="http://insecure-media.com/track.mp3"></audio>
            <form action="http://insecure-form.com/submit"></form>
            <a href="https://safe-link.com">Safe Link</a>
            </body>
            </html>
            """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };
    }

    private static HttpResponseMessage HandleSuspiciousLinksRequest()
    {
        var html = """
            <html>
            <head><title>Page With Suspicious Links</title></head>
            <body>
            <a href="https://malware-site.tk/payload" style="display:none">Hidden</a>
            <a href="https://phishing.xyz/login" style="visibility:hidden">Hidden2</a>
            <a href="https://scam.buzz/offer">Click here</a>
            <a href="https://legitimate-bank.com/account">https://my-bank.com/login</a>
            <a href="https://external1.com">Link 1</a>
            <a href="https://external2.com">Link 2</a>
            <a href="https://external3.com">Link 3</a>
            <a href="https://external4.com">Link 4</a>
            <a href="https://external5.com">Link 5</a>
            </body>
            </html>
            """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };
    }

    private static HttpResponseMessage HandleGenericRequest(HttpRequestMessage request)
    {
        var html = """
            <html>
            <head><title>Test Page</title></head>
            <body>
            <h1>Test Content</h1>
            <p>This is a test page for integration testing.</p>
            <a href="https://example.com/link1">Link 1</a>
            </body>
            </html>
            """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

        response.Headers.TryAddWithoutValidation("X-Content-Type-Options", "nosniff");
        response.Headers.TryAddWithoutValidation("X-Frame-Options", "DENY");
        response.Headers.TryAddWithoutValidation("Strict-Transport-Security", "max-age=31536000");

        return response;
    }
}

/// <summary>
/// WebApplicationFactory that replaces PostgreSQL with InMemory DB and intercepts
/// all outgoing HTTP calls to external APIs (Anthropic, BraveSearch).
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    public FakeExternalApiHandler FakeApiHandler { get; } = new();

    private readonly string _dbName = $"IntegrationTest_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true ||
                d.ImplementationType?.FullName?.Contains("Npgsql") == true)
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(b =>
                {
                    b.PrimaryHandler = FakeApiHandler;
                });
            });
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BraveSearch:ApiKey"] = "fake-brave-api-key-for-testing"
            });
        });
    }

    /// <summary>
    /// Seeds the in-memory database with test data.
    /// </summary>
    public async Task SeedDatabaseAsync(Action<AppDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }
}
