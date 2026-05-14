using Microsoft.EntityFrameworkCore;
using Moq;
using server.Checks.Security;
using server.Data;
using server.Helpers;
using server.Models;
using server.Services;

namespace server.Tests.Unit.Services;

public class SecurityCheckServiceRunAllTests
{
    private static (SecurityCheckService service, AppDbContext db) CreateService(params ISecurityCheck[] checks)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var httpFactory = Mock.Of<IHttpClientFactory>();
        var htmlExtractor = new HtmlTextExtractor(httpFactory);
        var scoreStore = new PageScoreStore(db, htmlExtractor);
        var service = new SecurityCheckService(checks, scoreStore);
        return (service, db);
    }

    [Fact]
    public async Task RunAllAsync_AllPass_Returns100()
    {
        var check1 = new Mock<ISecurityCheck>();
        check1.Setup(c => c.RunAsync(It.IsAny<string>()))
            .ReturnsAsync(new SecurityCheckResult { Severity = SecurityCheckSeverity.Pass, Title = "Test1" });
        var check2 = new Mock<ISecurityCheck>();
        check2.Setup(c => c.RunAsync(It.IsAny<string>()))
            .ReturnsAsync(new SecurityCheckResult { Severity = SecurityCheckSeverity.Pass, Title = "Test2" });

        var (service, db) = CreateService(check1.Object, check2.Object);
        var response = await service.RunAllAsync("https://example.com");

        Assert.Equal(100, response.OverallScore);
        Assert.Equal(2, response.Results.Count);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_AllWarning_Returns0()
    {
        var check = new Mock<ISecurityCheck>();
        check.Setup(c => c.RunAsync(It.IsAny<string>()))
            .ReturnsAsync(new SecurityCheckResult { Severity = SecurityCheckSeverity.Warning, Title = "Test" });

        var (service, db) = CreateService(check.Object);
        var response = await service.RunAllAsync("https://example.com");

        Assert.Equal(0, response.OverallScore);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_MixedSeverities_CalculatesCorrectAverage()
    {
        var pass = new Mock<ISecurityCheck>();
        pass.Setup(c => c.RunAsync(It.IsAny<string>()))
            .ReturnsAsync(new SecurityCheckResult { Severity = SecurityCheckSeverity.Pass, Title = "Pass" });
        var info = new Mock<ISecurityCheck>();
        info.Setup(c => c.RunAsync(It.IsAny<string>()))
            .ReturnsAsync(new SecurityCheckResult { Severity = SecurityCheckSeverity.Info, Title = "Info" });
        var warn = new Mock<ISecurityCheck>();
        warn.Setup(c => c.RunAsync(It.IsAny<string>()))
            .ReturnsAsync(new SecurityCheckResult { Severity = SecurityCheckSeverity.Warning, Title = "Warn" });

        var (service, db) = CreateService(pass.Object, info.Object, warn.Object);
        var response = await service.RunAllAsync("https://example.com");

        Assert.Equal(60, response.OverallScore);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_SavesScoreToDb()
    {
        var check = new Mock<ISecurityCheck>();
        check.Setup(c => c.RunAsync(It.IsAny<string>()))
            .ReturnsAsync(new SecurityCheckResult { Severity = SecurityCheckSeverity.Pass, Title = "Test" });

        var (service, db) = CreateService(check.Object);
        await service.RunAllAsync("https://example.com");

        var pageScore = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(pageScore);
        Assert.Equal(100, pageScore.SecurityScore);
        Assert.Equal(1, pageScore.SecurityCheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_SetsUrlInResponse()
    {
        var (service, db) = CreateService();
        var response = await service.RunAllAsync("https://test.com");

        Assert.Equal("https://test.com", response.Url);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_NoChecks_Returns100()
    {
        var (service, db) = CreateService();
        var response = await service.RunAllAsync("https://example.com");

        Assert.Equal(100, response.OverallScore);
        Assert.Empty(response.Results);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_AllInfo_Returns80()
    {
        var check1 = new Mock<ISecurityCheck>();
        check1.Setup(c => c.RunAsync(It.IsAny<string>()))
            .ReturnsAsync(new SecurityCheckResult { Severity = SecurityCheckSeverity.Info, Title = "Info1" });
        var check2 = new Mock<ISecurityCheck>();
        check2.Setup(c => c.RunAsync(It.IsAny<string>()))
            .ReturnsAsync(new SecurityCheckResult { Severity = SecurityCheckSeverity.Info, Title = "Info2" });

        var (service, db) = CreateService(check1.Object, check2.Object);
        var response = await service.RunAllAsync("https://example.com");

        Assert.Equal(80, response.OverallScore);
        db.Dispose();
    }
}
