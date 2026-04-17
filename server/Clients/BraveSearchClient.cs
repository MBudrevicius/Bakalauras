using System.Text.Json;
using System.Web;
using server.Models;

namespace server.Clients;

public class BraveSearchClient
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public BraveSearchClient(IConfiguration config, IHttpClientFactory httpFactory)
    {
        _config = config;
        _httpFactory = httpFactory;
    }

    public async Task<List<BraveSearchResult>> SearchAsync(string query, int count = 8)
    {
        var apiKey = _config["BraveSearch:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return [];
        }

        try
        {
            var client = _httpFactory.CreateClient();
            var encoded = HttpUtility.UrlEncode(query);
            var requestUrl = $"https://api.search.brave.com/res/v1/web/search?q={encoded}&count={count}";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("X-Subscription-Token", apiKey);
            request.Headers.Add("Accept", "application/json");

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("web", out var web)
                || !web.TryGetProperty("results", out var items))
            {
                return [];
            }

            var results = new List<BraveSearchResult>();
            foreach (var item in items.EnumerateArray())
            {
                var url = item.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
                var snippet = item.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                results.Add(new BraveSearchResult
                {
                    Url = url,
                    Title = HttpUtility.HtmlDecode(title),
                    Snippet = HttpUtility.HtmlDecode(snippet),
                });
            }

            return results;
        }
        catch
        {
            return [];
        }
    }
}
