using Microsoft.EntityFrameworkCore;
using Moq;
using server.Data;
using server.Helpers;
using server.Models;
using server.Services;

namespace server.Tests.Unit.Services;

public class PageScoreStoreSaveTests
{
    private static (PageScoreStore store, AppDbContext db) CreateStore()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var httpFactory = Mock.Of<IHttpClientFactory>();
        var htmlExtractor = new HtmlTextExtractor(httpFactory);
        return (new PageScoreStore(db, htmlExtractor), db);
    }

    [Fact]
    public async Task SavePageScoreAsync_NewDomain_CreatesRecord()
    {
        var (store, db) = CreateStore();

        await store.SavePageScoreAsync("https://example.com", securityScore: 80, credibilityScore: null, aiScore: null);

        var record = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(record);
        Assert.Equal(80, record.SecurityScore);
        Assert.Equal(1, record.SecurityCheckCount);
        Assert.Equal(0, record.CredibilityCheckCount);
        Assert.Equal(0, record.AiCheckCount);
        Assert.Equal(1, record.CheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task SavePageScoreAsync_ExistingDomain_UpdatesAverage()
    {
        var (store, db) = CreateStore();

        await store.SavePageScoreAsync("https://example.com", securityScore: 60, credibilityScore: null, aiScore: null);
        await store.SavePageScoreAsync("https://example.com", securityScore: 80, credibilityScore: null, aiScore: null);

        var record = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(record);
        // (60 * 1 + 80) / 2 = 70
        Assert.Equal(70, record.SecurityScore);
        Assert.Equal(2, record.SecurityCheckCount);
        Assert.Equal(2, record.CheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task SavePageScoreAsync_CredibilityScore_Saved()
    {
        var (store, db) = CreateStore();

        await store.SavePageScoreAsync("https://example.com", securityScore: null, credibilityScore: 75, aiScore: null);

        var record = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(record);
        Assert.Equal(75, record.CredibilityScore);
        Assert.Equal(1, record.CredibilityCheckCount);
        Assert.Equal(0, record.SecurityCheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task SavePageScoreAsync_AiScore_Saved()
    {
        var (store, db) = CreateStore();

        await store.SavePageScoreAsync("https://example.com", securityScore: null, credibilityScore: null, aiScore: 55);

        var record = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(record);
        Assert.Equal(55, record.AiScore);
        Assert.Equal(1, record.AiCheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task SavePageScoreAsync_AllThreeScores_SavedCorrectly()
    {
        var (store, db) = CreateStore();

        await store.SavePageScoreAsync("https://example.com", securityScore: 90, credibilityScore: 80, aiScore: 30);

        var record = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(record);
        Assert.Equal(90, record.SecurityScore);
        Assert.Equal(80, record.CredibilityScore);
        Assert.Equal(30, record.AiScore);
        Assert.Equal(1, record.SecurityCheckCount);
        Assert.Equal(1, record.CredibilityCheckCount);
        Assert.Equal(1, record.AiCheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task SavePageScoreAsync_NullScores_DoesNotIncrementCounts()
    {
        var (store, db) = CreateStore();

        // First save with security
        await store.SavePageScoreAsync("https://example.com", securityScore: 80, credibilityScore: null, aiScore: null);
        // Second save with credibility only
        await store.SavePageScoreAsync("https://example.com", securityScore: null, credibilityScore: 70, aiScore: null);

        var record = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(record);
        Assert.Equal(80, record.SecurityScore); // unchanged
        Assert.Equal(1, record.SecurityCheckCount); // not incremented
        Assert.Equal(70, record.CredibilityScore);
        Assert.Equal(1, record.CredibilityCheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task SavePageScoreAsync_ExistingDomain_UpdatesAiAverage()
    {
        var (store, db) = CreateStore();
        await store.SavePageScoreAsync("https://example.com", securityScore: null, credibilityScore: null, aiScore: 40);
        await store.SavePageScoreAsync("https://example.com", securityScore: null, credibilityScore: null, aiScore: 80);
        var record = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(record);
        Assert.Equal(60, record.AiScore); // (40*1+80)/2 = 60
        Assert.Equal(2, record.AiCheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task SavePageScoreAsync_ExistingDomain_UpdatesCredibilityAverage()
    {
        var (store, db) = CreateStore();
        await store.SavePageScoreAsync("https://example.com", securityScore: null, credibilityScore: 50, aiScore: null);
        await store.SavePageScoreAsync("https://example.com", securityScore: null, credibilityScore: 90, aiScore: null);
        var record = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(record);
        Assert.Equal(70, record.CredibilityScore); // (50*1+90)/2 = 70
        Assert.Equal(2, record.CredibilityCheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task GetPageWithRelatedScoreAsync_NoRecord_ReturnsNull()
    {
        var (store, db) = CreateStore();

        var result = await store.GetPageWithRelatedScoreAsync("https://nonexistent.com");
        Assert.Null(result);
        db.Dispose();
    }

    [Fact]
    public async Task GetPageWithRelatedScoreAsync_ExistingRecord_ReturnsIt()
    {
        var (store, db) = CreateStore();

        await store.SavePageScoreAsync("https://example.com", securityScore: 85, credibilityScore: null, aiScore: null);
        var result = await store.GetPageWithRelatedScoreAsync("https://example.com");

        Assert.NotNull(result);
        Assert.Equal(85, result.SecurityScore);
        db.Dispose();
    }

    [Fact]
    public async Task SavePageScoreAsync_MultipleUpdates_RunningAverage()
    {
        var (store, db) = CreateStore();

        await store.SavePageScoreAsync("https://example.com", securityScore: 60, credibilityScore: null, aiScore: null);
        await store.SavePageScoreAsync("https://example.com", securityScore: 80, credibilityScore: null, aiScore: null);
        await store.SavePageScoreAsync("https://example.com", securityScore: 100, credibilityScore: null, aiScore: null);

        var record = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(record);
        Assert.Equal(3, record!.SecurityCheckCount);
        Assert.Equal(3, record.CheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task SavePageScoreAsync_DifferentDomains_SeparateRecords()
    {
        var (store, db) = CreateStore();

        await store.SavePageScoreAsync("https://site1.com", securityScore: 80, credibilityScore: null, aiScore: null);
        await store.SavePageScoreAsync("https://site2.com", securityScore: 60, credibilityScore: null, aiScore: null);

        var count = await db.PageScores.CountAsync();
        Assert.Equal(2, count);
        db.Dispose();
    }

    [Fact]
    public async Task SavePageScoreAsync_UrlWithPath_ExtractsDomain()
    {
        var (store, db) = CreateStore();

        await store.SavePageScoreAsync("https://example.com/path/to/page?q=1", securityScore: 70, credibilityScore: null, aiScore: null);

        var record = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(record);
        db.Dispose();
    }
}
