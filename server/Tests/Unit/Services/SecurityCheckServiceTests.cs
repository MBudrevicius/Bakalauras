using server.Models;
using server.Services;

namespace server.Tests.Unit.Services;

public class SecurityCheckServiceTests
{
    [Fact]
    public void CalculateScore_AllPass_Returns100()
    {
        var results = new[]
        {
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Pass },
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Pass },
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Pass },
        };

        var score = InvokeCalculateScore(results);
        Assert.Equal(100, score);
    }

    [Fact]
    public void CalculateScore_AllWarning_Returns0()
    {
        var results = new[]
        {
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Warning },
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Warning },
        };

        var score = InvokeCalculateScore(results);
        Assert.Equal(0, score);
    }

    [Fact]
    public void CalculateScore_MixedResults_ReturnsWeightedAverage()
    {
        var results = new[]
        {
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Pass },    // 100
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Info },     // 80
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Warning },  // 0
        };

        var score = InvokeCalculateScore(results);
        Assert.Equal(60, score); // (100 + 80 + 0) / 3 = 60
    }

    [Fact]
    public void CalculateScore_EmptyResults_Returns100()
    {
        var score = InvokeCalculateScore([]);
        Assert.Equal(100, score);
    }

    [Fact]
    public void CalculateScore_AllInfo_Returns80()
    {
        var results = new[]
        {
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Info },
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Info },
        };

        var score = InvokeCalculateScore(results);
        Assert.Equal(80, score);
    }

    [Fact]
    public void CalculateScore_SinglePass_Returns100()
    {
        var results = new[]
        {
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Pass },
        };

        var score = InvokeCalculateScore(results);
        Assert.Equal(100, score);
    }

    [Fact]
    public void CalculateScore_SingleWarning_Returns0()
    {
        var results = new[]
        {
            new SecurityCheckResult { Severity = SecurityCheckSeverity.Warning },
        };

        var score = InvokeCalculateScore(results);
        Assert.Equal(0, score);
    }

    private static int InvokeCalculateScore(SecurityCheckResult[] results)
    {
        var method = typeof(SecurityCheckService).GetMethod("CalculateScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (int)method!.Invoke(null, [results])!;
    }
}
