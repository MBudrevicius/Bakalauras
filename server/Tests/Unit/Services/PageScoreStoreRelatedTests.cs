using System.Net;
using Microsoft.EntityFrameworkCore;
using Moq;
using server.Data;
using server.Helpers;
using server.Models;
using server.Services;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Services;

public class PageScoreStoreRelatedTests
{
    private static (PageScoreStore store, AppDbContext db) CreateStoreWithExtractor(MockHttpMessageHandler handler)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var factory = new MockHttpClientFactory(handler);
        var htmlExtractor = new HtmlTextExtractor(factory);
        return (new PageScoreStore(db, htmlExtractor), db);
    }

    [Fact]
    public async Task GetPageWithRelatedScoreAsync_NoLinks_ReturnsOwnScore()
    {
        var html = "<html><body><p>No links here</p></body></html>";
        var handler = new MockHttpMessageHandler(html);
        var (store, db) = CreateStoreWithExtractor(handler);

        await store.SavePageScoreAsync("https://example.com", securityScore: 80, credibilityScore: 70, aiScore: 30);
        var result = await store.GetPageWithRelatedScoreAsync("https://example.com");

        Assert.NotNull(result);
        Assert.Equal(80, result!.SecurityScore);
        Assert.Equal(70, result.CredibilityScore);
        Assert.Equal(30, result.AiScore);
        db.Dispose();
    }

    [Fact]
    public async Task GetPageWithRelatedScoreAsync_WithRelatedPages_BlendScores()
    {
        var html = "<html><body><a href=\"https://related.com/page\">Link</a></body></html>";
        var handler = new MockHttpMessageHandler(html);
        var (store, db) = CreateStoreWithExtractor(handler);

        await store.SavePageScoreAsync("https://example.com", securityScore: 80, credibilityScore: 60, aiScore: 40);
        await store.SavePageScoreAsync("https://related.com", securityScore: 60, credibilityScore: 80, aiScore: 20);

        var result = await store.GetPageWithRelatedScoreAsync("https://example.com");

        Assert.NotNull(result);
        Assert.Equal(78, result!.SecurityScore);
        Assert.Equal(62, result.CredibilityScore);
        Assert.Equal(38, result.AiScore);
        db.Dispose();
    }

    [Fact]
    public async Task GetPageWithRelatedScoreAsync_RelatedPageNotInDb_IgnoresIt()
    {
        var html = "<html><body><a href=\"https://unknown.com/page\">Link</a></body></html>";
        var handler = new MockHttpMessageHandler(html);
        var (store, db) = CreateStoreWithExtractor(handler);

        await store.SavePageScoreAsync("https://example.com", securityScore: 90, credibilityScore: null, aiScore: null);

        var result = await store.GetPageWithRelatedScoreAsync("https://example.com");

        Assert.NotNull(result);
        Assert.Equal(90, result!.SecurityScore);
        db.Dispose();
    }

    [Fact]
    public async Task GetPageWithRelatedScoreAsync_HttpError_ReturnsOwnScore()
    {
        var handler = new MockHttpMessageHandler("error", HttpStatusCode.InternalServerError);
        var (store, db) = CreateStoreWithExtractor(handler);

        await store.SavePageScoreAsync("https://example.com", securityScore: 75, credibilityScore: null, aiScore: null);

        var result = await store.GetPageWithRelatedScoreAsync("https://example.com");

        Assert.NotNull(result);
        Assert.Equal(75, result!.SecurityScore);
        db.Dispose();
    }

    [Fact]
    public async Task GetPageWithRelatedScoreAsync_NotFound_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler("<html></html>");
        var (store, db) = CreateStoreWithExtractor(handler);

        var result = await store.GetPageWithRelatedScoreAsync("https://nonexistent.com");
        Assert.Null(result);
        db.Dispose();
    }
}
