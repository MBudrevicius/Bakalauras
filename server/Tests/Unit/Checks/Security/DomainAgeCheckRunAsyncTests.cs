using Microsoft.Extensions.Logging.Abstractions;
using server.Checks.Security;
using server.Models;

namespace server.Tests.Unit.Checks.Security;

public class DomainAgeCheckRunAsyncTests
{
    [Fact]
    public async Task RunAsync_InvalidUrl_ReturnsInfo()
    {
        var check = new DomainAgeCheck(NullLogger<DomainAgeCheck>.Instance);
        var result = await check.RunAsync("not-a-url");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("Could not parse", result.Description);
    }

    [Fact]
    public void RunAsync_CorrectType()
    {
        var check = new DomainAgeCheck(NullLogger<DomainAgeCheck>.Instance);
        Assert.Equal(SecurityCheckType.DomainAge, check.Type);
    }
}
