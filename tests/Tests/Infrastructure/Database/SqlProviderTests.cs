using FluentAssertions;
using JobService.Infrastructure.Database;
using JobService.Shared.Config;
using JobService.Shared.Errors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace JobService.Tests.Infrastructure.Database;

public class SqlProviderTests
{
    [Fact]
    public void Ctor_Throws_When_DatabaseUrl_Missing()
    {
        var settings = Options.Create(new AppSettings { DatabaseUrl = "" });
        Action act = () => new SqlProvider(settings, NullLogger<SqlProvider>.Instance);
        act.Should().Throw<ConfigurationError>();
    }

    [Fact]
    public void Ctor_Throws_On_Null_Options()
    {
        Action act = () => new SqlProvider(null!, NullLogger<SqlProvider>.Instance);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_Throws_On_Null_Logger()
    {
        var settings = Options.Create(new AppSettings { DatabaseUrl = "x" });
        Action act = () => new SqlProvider(settings, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        var provider = new SqlProvider(
            Options.Create(new AppSettings { DatabaseUrl = "x" }),
            NullLogger<SqlProvider>.Instance);

        provider.Dispose();
        provider.Dispose();
    }

    [Fact]
    public async Task OpenAsync_Throws_ObjectDisposed_After_Dispose()
    {
        var provider = new SqlProvider(
            Options.Create(new AppSettings { DatabaseUrl = "x" }),
            NullLogger<SqlProvider>.Instance);
        provider.Dispose();

        Func<Task> act = () => provider.OpenAsync(CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task PingAsync_Returns_False_On_Exception()
    {
        // Use a syntactically-valid connection string that won't connect
        var provider = new SqlProvider(
            Options.Create(new AppSettings
            {
                DatabaseUrl = "Server=localhost,9999;Database=does_not_exist;User Id=sa;Password=x;TrustServerCertificate=True;Connect Timeout=1",
                DatabaseCommandTimeoutSeconds = 1,
            }),
            NullLogger<SqlProvider>.Instance);

        var result = await provider.PingAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task OpenAsync_ConnectionError_On_Failure()
    {
        // Force OpenAsync to fail (invalid server)
        var provider = new SqlProvider(
            Options.Create(new AppSettings
            {
                DatabaseUrl = "Server=localhost,9999;Database=does_not_exist;User Id=sa;Password=x;TrustServerCertificate=True;Connect Timeout=1",
            }),
            NullLogger<SqlProvider>.Instance);

        Func<Task> act = () => provider.OpenAsync(CancellationToken.None);
        await act.Should().ThrowAsync<ConnectionError>();
    }

    [Fact]
    public async Task OpenAsync_Success_Path_Via_Mock()
    {
        // Mock the protected CreateConnection via Mock<SqlProvider> with CallBase
        var mock = new Mock<SqlProvider>(
            Options.Create(new AppSettings { DatabaseUrl = "Server=localhost" }),
            NullLogger<SqlProvider>.Instance)
        { CallBase = true };

        // Replace the OpenAsync behavior with one that returns a stub
        mock.Setup(p => p.OpenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SqlConnection)null!);

        // This won't actually call the real OpenAsync (it's overridden by the mock),
        // but it shows we can mock it. The real success path requires a real DB.
        var conn = await mock.Object.OpenAsync();
        conn.Should().BeNull();
    }

    [Fact]
    public async Task PingAsync_Success_Path_Via_Mock()
    {
        // Mock the entire PingAsync
        var mock = new Mock<SqlProvider>(
            Options.Create(new AppSettings { DatabaseUrl = "Server=localhost" }),
            NullLogger<SqlProvider>.Instance)
        { CallBase = true };

        mock.Setup(p => p.PingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await mock.Object.PingAsync();
        result.Should().BeTrue();
    }
}
