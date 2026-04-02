using System.Collections.Concurrent;
using System.Text.Json;
using server.Models;

namespace server.Services;

/// <summary>
/// Lightweight persistent store for page scores. Uses a JSON file.
/// Registered as a singleton so all services share the same in-memory cache.
/// </summary>
public class PageScoreStore
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, PageScore> _scores;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public PageScoreStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "page_scores.json");
        _scores = Load();
    }

    /// <summary>Normalise URL for use as dictionary key (lowercase, trim trailing slash).</summary>
    private static string NormalizeUrl(string url)
    {
        url = url.Trim().TrimEnd('/');
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.GetLeftPart(UriPartial.Path).ToLowerInvariant()
                   + uri.Query.ToLowerInvariant();
        return url.ToLowerInvariant();
    }

    public PageScore? Get(string url)
    {
        _scores.TryGetValue(NormalizeUrl(url), out var score);
        return score;
    }

    public List<PageScore> GetAll() => _scores.Values.ToList();

    /// <summary>Save or update a page score. Merges with existing data.</summary>
    public async Task SaveAsync(string url, int? securityScore, int? aiScore)
    {
        var key = NormalizeUrl(url);
        var domain = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "";

        _scores.AddOrUpdate(key,
            _ => new PageScore
            {
                Url = url,
                Domain = domain,
                SecurityScore = securityScore ?? 0,
                AiScore = aiScore ?? 0,
                LastChecked = DateTime.UtcNow,
                CheckCount = 1
            },
            (_, existing) =>
            {
                if (securityScore.HasValue) existing.SecurityScore = securityScore.Value;
                if (aiScore.HasValue) existing.AiScore = aiScore.Value;
                existing.LastChecked = DateTime.UtcNow;
                existing.CheckCount++;
                return existing;
            });

        await PersistAsync();
    }

    /// <summary>
    /// Calculate adjusted score factoring in related pages.
    /// Formula: 90% own score + 10% average of related pages.
    /// </summary>
    public static int CalculateAdjustedScore(int ownScore, IEnumerable<int> relatedScores)
    {
        var related = relatedScores.ToList();
        if (related.Count == 0) return ownScore;

        var relatedAvg = (double)related.Sum() / related.Count;
        return (int)Math.Round(ownScore * 0.9 + relatedAvg * 0.1);
    }

    private ConcurrentDictionary<string, PageScore> Load()
    {
        if (!File.Exists(_filePath))
            return new ConcurrentDictionary<string, PageScore>();

        try
        {
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<PageScore>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var dict = new ConcurrentDictionary<string, PageScore>();
            foreach (var s in list ?? [])
                dict[NormalizeUrl(s.Url)] = s;
            return dict;
        }
        catch
        {
            return new ConcurrentDictionary<string, PageScore>();
        }
    }

    private async Task PersistAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_scores.Values.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
