using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Helpers;
using server.Models;

namespace server.Services;

public class PageScoreStore
{
    private readonly AppDbContext _context;
    private readonly HtmlTextExtractor _htmlExtractor;

    public PageScoreStore(AppDbContext context, HtmlTextExtractor htmlExtractor)
    {
        _context = context;
        _htmlExtractor = htmlExtractor;
    }

    private static string ExtractDomain(string url)
    {
        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return uri.Host.ToLowerInvariant();
        }
        return url.Trim().ToLowerInvariant();
    }

    private async Task<PageScore?> GetPageScoreAsync(string url)
    {
        var domain = ExtractDomain(url);
        return await _context.PageScores.FirstOrDefaultAsync(p => p.Domain == domain);
    }

    public async Task SavePageScoreAsync(string url, int? securityScore, int? aiScore)
    {
        var domain = ExtractDomain(url);

        var existingScore = await _context.PageScores.FirstOrDefaultAsync(p => p.Domain == domain);

        if (existingScore == null)
        {
            var newScore = new PageScore
            {
                Url = domain,
                Domain = domain,
                SecurityScore = securityScore ?? 0,
                AiScore = aiScore ?? 0,
                SecurityCheckCount = securityScore.HasValue ? 1 : 0,
                AiCheckCount = aiScore.HasValue ? 1 : 0,
                LastChecked = DateTime.UtcNow,
                CheckCount = 1,
            };
            _context.PageScores.Add(newScore);
        }
        else
        {
            if (securityScore.HasValue)
            {
                var count = existingScore.SecurityCheckCount;
                existingScore.SecurityScore = (int)Math.Round((existingScore.SecurityScore * (double)count + securityScore.Value) / (count + 1));
                existingScore.SecurityCheckCount++;
            }
            if (aiScore.HasValue)
            {
                var count = existingScore.AiCheckCount;
                existingScore.AiScore = (int)Math.Round((existingScore.AiScore * (double)count + aiScore.Value) / (count + 1));
                existingScore.AiCheckCount++;
            }
            existingScore.LastChecked = DateTime.UtcNow;
            existingScore.CheckCount++;
        }

        await _context.SaveChangesAsync();
    }

    private static int CalculatePageWithRelatedPagesScore(int ownScore, IEnumerable<int> relatedScores)
    {
        var related = relatedScores.ToList();
        if (related.Count == 0) return ownScore;

        var relatedAvg = (double)related.Sum() / related.Count;
        return (int)Math.Round(ownScore * 0.9 + relatedAvg * 0.1);
    }

    public async Task<PageScore?> GetPageWithRelatedScoreAsync(string url)
    {
        var pageScore = await GetPageScoreAsync(url);
        if (pageScore == null) 
        {
            return null;
        }

        var links = await _htmlExtractor.ExtractLinksFromUrl(url);
        if (links.Count == 0)
        {
            return pageScore;
        }

        var relSecScores = new List<int>();
        var relAiScores = new List<int>();

        foreach (var link in links)
        {
            var stored = await GetPageScoreAsync(link);
            if (stored == null)
            {
                continue;
            }
            relSecScores.Add(stored.SecurityScore);
            relAiScores.Add(stored.AiScore);
        }

        pageScore.SecurityScore = CalculatePageWithRelatedPagesScore(pageScore.SecurityScore, relSecScores);
        pageScore.AiScore = CalculatePageWithRelatedPagesScore(pageScore.AiScore, relAiScores);

        return pageScore;
    }
}
