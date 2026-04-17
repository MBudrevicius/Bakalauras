using server.Checks.Security;
using server.Models;

namespace server.Services;

public class SecurityCheckService
{
    private readonly IEnumerable<ISecurityCheck> _checks;
    private readonly PageScoreStore _scoreStore;

    public SecurityCheckService(IEnumerable<ISecurityCheck> checks, PageScoreStore scoreStore)
    {
        _checks = checks;
        _scoreStore = scoreStore;
    }

    public async Task<SecurityCheckResponse> RunAllAsync(string url)
    {
        var tasks = _checks.Select(c => c.RunAsync(url));
        var results = await Task.WhenAll(tasks);

        var response = new SecurityCheckResponse
        {
            Url = url,
            Results = [.. results],
            OverallScore = CalculateScore(results)
        };

        await _scoreStore.SavePageScoreAsync(url, securityScore: response.OverallScore, aiScore: null);

        return response;
    }

    private static int CalculateScore(SecurityCheckResult[] results)
    {
        if (results.Length == 0)
        {
            return 100;
        }

        int score = 0;
        foreach (var result in results)
        {
            score += result.Severity switch
            {
                SecurityCheckSeverity.Pass => 100,
                SecurityCheckSeverity.Info => 80,
                SecurityCheckSeverity.Warning => 0,
                _ => 100
            };
        }

        return score / results.Length;
    }
}
