using JobService.Core;
using JobService.Infrastructure.Health;
using JobService.Shared.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobService.Jobs;

public static class RegisterJobs
{
    public static IServiceCollection AddJobs(this IServiceCollection services)
    {
        services.AddSingleton<HealthCheckJob>(sp => new HealthCheckJob(
            sp.GetRequiredService<IHealthChecker>(),
            sp.GetRequiredService<IOptions<AppSettings>>()));
        return services;
    }

    public static BaseJob[] ResolveJobs(IServiceProvider sp)
    {
        var healthCheck = sp.GetRequiredService<HealthCheckJob>();
        var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
        if (!settings.HealthCheckEnabled)
            healthCheck.Enabled = false;
        return new BaseJob[] { healthCheck };
    }
}
