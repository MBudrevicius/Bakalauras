using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;

namespace server.Services;

/// <summary>
/// Persistent store for page scores using EF Core with NeonDB (PostgreSQL).
/// Supports both anonymous and authenticated user tracking.
/// </summary>
public class PageScoreStore
{
    private readonly AppDbContext _context;

    public PageScoreStore(AppDbContext context)
    {
        _context = context;
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

    public async Task<PageScore?> GetAsync(string url, int? userId = null)
    {
        var normalizedUrl = NormalizeUrl(url);
        return await _context.PageScores
            .FirstOrDefaultAsync(p => p.Url == normalizedUrl && (userId == null || p.UserId == userId));
    }

    public async Task<List<PageScore>> GetAllAsync(int? userId = null)
    {
        var query = _context.PageScores.AsQueryable();
        if (userId.HasValue)
            query = query.Where(p => p.UserId == userId);
        return await query.ToListAsync();
    }

    /// <summary>Save or update a page score. Merges with existing data.</summary>
    public async Task SaveAsync(string url, int? securityScore, int? aiScore, int? userId = null)
    {
        var normalizedUrl = NormalizeUrl(url);
        var domain = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "";

        var existingScore = await _context.PageScores
            .FirstOrDefaultAsync(p => p.Url == normalizedUrl && p.UserId == userId);

        if (existingScore == null)
        {
            var newScore = new PageScore
            {
                Url = url,
                Domain = domain,
                SecurityScore = securityScore ?? 0,
                AiScore = aiScore ?? 0,
                LastChecked = DateTime.UtcNow,
                CheckCount = 1,
                UserId = userId
            };

            _context.PageScores.Add(newScore);
        }
        else
        {
            if (securityScore.HasValue) existingScore.SecurityScore = securityScore.Value;
            if (aiScore.HasValue) existingScore.AiScore = aiScore.Value;
            existingScore.LastChecked = DateTime.UtcNow;
            existingScore.CheckCount++;
        }

        await _context.SaveChangesAsync();
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
}
