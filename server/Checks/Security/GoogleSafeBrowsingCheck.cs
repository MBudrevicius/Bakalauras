using System.Text;
using System.Text.Json;
using server.Models;

namespace server.Checks.Security;

public class GoogleSafeBrowsingCheck : ISecurityCheck
{
    public SecurityCheckType Type => SecurityCheckType.GoogleSafeBrowsing;

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleSafeBrowsingCheck> _logger;

    public GoogleSafeBrowsingCheck(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<GoogleSafeBrowsingCheck> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<SecurityCheckResult> RunAsync(string url)
    {
        var apiKey = _config["GoogleSafeBrowsing:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return CheckResult(SecurityCheckSeverity.Info, "Google Safe Browsing check failed.");
        }

        try
        {
            var requestBody = new
            {
                client = new { clientId = "secure-web", clientVersion = "0.1.0" },
                threatInfo = new
                {
                    threatTypes = new[] { "MALWARE", "SOCIAL_ENGINEERING", "UNWANTED_SOFTWARE", "POTENTIALLY_HARMFUL_APPLICATION" },
                    platformTypes = new[] { "ANY_PLATFORM" },
                    threatEntryTypes = new[] { "URL" },
                    threatEntries = new[] { new { url } }
                }
            };

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                $"https://safebrowsing.googleapis.com/v4/threatMatches:find?key={apiKey}",
                content);

            if (!response.IsSuccessStatusCode)
            {
                return CheckResult(SecurityCheckSeverity.Info, $"API returned status {(int)response.StatusCode}. Could not complete check.");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("matches", out var matches) && matches.GetArrayLength() > 0)
            {
                var threats = new List<string>();
                foreach (var match in matches.EnumerateArray())
                {
                    if (match.TryGetProperty("threatType", out var tt))
                    {
                        threats.Add(FormatThreatType(tt.GetString() ?? "UNKNOWN"));
                    }
                }

                return CheckResult(SecurityCheckSeverity.Warning, $"URL flagged: {string.Join(", ", threats.Distinct())}.");
            }

            return CheckResult(SecurityCheckSeverity.Pass, "No threats found by Google Safe Browsing.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Safe Browsing check failed for {Url}", url);
            return CheckResult(SecurityCheckSeverity.Info, "Google Safe Browsing check failed.");
        }
    }

    private static string FormatThreatType(string raw) => raw switch
    {
        "MALWARE" => "Malware",
        "SOCIAL_ENGINEERING" => "Social Engineering / Phishing",
        "UNWANTED_SOFTWARE" => "Unwanted Software",
        "POTENTIALLY_HARMFUL_APPLICATION" => "Potentially Harmful Application",
        _ => raw
    };

    private SecurityCheckResult CheckResult(SecurityCheckSeverity severity, string description) => new()
    {
        Type = Type,
        Severity = severity,
        Title = "Google Safe Browsing",
        Description = description
    };
}
