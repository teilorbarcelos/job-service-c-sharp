using FluentAssertions;
using JobService.Infrastructure.Health;
using JobService.Jobs;
using JobService.Shared.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace JobService.Tests.Jobs;

public class RegisterJobsTests
{
    [Fact]
    public void ResolveJobs_Returns_HealthCheckJob_Enabled_When_Setting_True()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<IHealthChecker>(new Mock<IHealthChecker>().Object);
        sc.AddSingleton<IOptions<AppSettings>>(Options.Create(new AppSettings { HealthCheckEnabled = true }));
        sc.AddJobs();
        sc.AddSingleton<HealthCheckJob>(sp => new HealthCheckJob(
            sp.GetRequiredService<IHealthChecker>(),
            sp.GetRequiredService<IOptions<AppSettings>>()));
        var sp = sc.BuildServiceProvider();

        var jobs = RegisterJobs.ResolveJobs(sp);

        jobs.Should().HaveCount(1);
        jobs[0].Name.Should().Be("health-check");
        jobs[0].Enabled.Should().BeTrue();
    }

    [Fact]
    public void ResolveJobs_Disables_HealthCheckJob_When_Setting_False()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<IHealthChecker>(new Mock<IHealthChecker>().Object);
        sc.AddSingleton<IOptions<AppSettings>>(Options.Create(new AppSettings { HealthCheckEnabled = false }));
        sc.AddJobs();
        sc.AddSingleton<HealthCheckJob>(sp => new HealthCheckJob(
            sp.GetRequiredService<IHealthChecker>(),
            sp.GetRequiredService<IOptions<AppSettings>>()));
        var sp = sc.BuildServiceProvider();

        var jobs = RegisterJobs.ResolveJobs(sp);

        jobs[0].Enabled.Should().BeFalse();
    }

    [Fact]
    public void AddJobs_Can_Be_Called_Multiple_Times()
    {
        var sc = new ServiceCollection();
        sc.AddJobs();
        sc.AddJobs();
        sc.Should().NotBeEmpty();
    }
}
