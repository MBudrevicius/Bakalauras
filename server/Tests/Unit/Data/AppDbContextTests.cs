using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;

namespace server.Tests.Unit.Data;

public class AppDbContextTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task PageScores_CanAddAndRetrieve()
    {
        using var db = CreateContext();
        db.PageScores.Add(new PageScore
        {
            Domain = "example.com",
            Url = "example.com",
            SecurityScore = 80,
            CheckCount = 1
        });
        await db.SaveChangesAsync();

        var result = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "example.com");
        Assert.NotNull(result);
        Assert.Equal(80, result.SecurityScore);
    }

    [Fact]
    public async Task PageScores_DomainUnique_ThrowsOnDuplicate()
    {
        using var db = CreateContext();
        db.PageScores.Add(new PageScore { Domain = "test.com", Url = "test.com" });
        await db.SaveChangesAsync();

        db.PageScores.Add(new PageScore { Domain = "test.com", Url = "test.com" });
        var entity = db.Model.FindEntityType(typeof(PageScore));
        Assert.NotNull(entity);
        var domainIndex = entity!.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == "Domain"));
        Assert.NotNull(domainIndex);
        Assert.True(domainIndex!.IsUnique);
    }

    [Fact]
    public void OnModelCreating_DomainIsRequired()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(PageScore));
        Assert.NotNull(entity);
        var domainProp = entity!.FindProperty("Domain");
        Assert.NotNull(domainProp);
        Assert.False(domainProp!.IsNullable);
    }

    [Fact]
    public void OnModelCreating_UrlHasMaxLength()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(PageScore));
        var urlProp = entity!.FindProperty("Url");
        Assert.Equal(2048, urlProp!.GetMaxLength());
    }

    [Fact]
    public void OnModelCreating_DomainHasMaxLength()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(PageScore));
        var domainProp = entity!.FindProperty("Domain");
        Assert.Equal(255, domainProp!.GetMaxLength());
    }

    [Fact]
    public void OnModelCreating_HasPrimaryKey()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(PageScore));
        var pk = entity!.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Equal("Id", pk!.Properties.Single().Name);
    }
}
