using JobService.Core;
using JobService.Infrastructure.Database;
using JobService.Infrastructure.Health;
using JobService.Infrastructure.Messaging;
using JobService.Infrastructure.Redis;
using JobService.Jobs;
using JobService.Shared.Config;
using JobService.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

Log.Logger = AppLogger.Create();

try
{
    Log.Information("Starting Job Service...");

    var settings = EnvValidator.Load();
    Log.Logger = AppLogger.Create(settings.LogLevel, settings.Environment);

    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    builder.Services.AddSingleton(settings);
    builder.Services.AddSingleton<ISqlProvider, SqlProvider>();
    builder.Services.AddSingleton<IRedisProvider, RedisProvider>();
    builder.Services.AddSingleton<IRabbitMqProvider, RabbitMqProvider>();
    builder.Services.AddSingleton<IHealthChecker, DefaultHealthChecker>();
    builder.Services.AddSingleton<ICronAdapter, CronosAdapter>();

    builder.Services.AddJobs();
    builder.Services.AddSingleton<Scheduler>(sp =>
    {
        var jobs = RegisterJobs.ResolveJobs(sp);
        var cron = sp.GetRequiredService<ICronAdapter>();
        var timeout = TimeSpan.FromSeconds(settings.JobExecutionTimeoutSeconds);
        return new Scheduler(jobs, cron, timeout, Log.Logger);
    });
    builder.Services.AddHostedService(sp => sp.GetRequiredService<Scheduler>());

    builder.Services.Configure<HostOptions>(o =>
        o.ShutdownTimeout = TimeSpan.FromSeconds(settings.ShutdownTimeoutSeconds));

    var host = builder.Build();

    var shutdownHandler = new ShutdownHandler(
        Log.Logger,
        host.Services.GetRequiredService<IHostApplicationLifetime>(),
        TimeSpan.FromSeconds(settings.ShutdownTimeoutSeconds),
        async () =>
        {
            host.Services.GetRequiredService<Scheduler>().StopAsync(default).GetAwaiter().GetResult();
            if (settings.MessagingEnabled)
            {
                var rabbit = host.Services.GetRequiredService<IRabbitMqProvider>();
                rabbit.Disconnect();
            }
            host.Services.GetRequiredService<IRedisProvider>().Dispose();
            host.Services.GetRequiredService<ISqlProvider>().Dispose();
            await Task.CompletedTask;
        });
    shutdownHandler.Register();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    await Log.CloseAndFlushAsync();
}
