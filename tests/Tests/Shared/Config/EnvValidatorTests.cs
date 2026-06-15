using FluentAssertions;
using JobService.Shared.Config;
using JobService.Shared.Errors;
using Xunit;

namespace JobService.Tests.Shared.Config;

public class EnvValidatorTests
{
    [Fact]
    public void GetEnv_Returns_Env_Value_When_Set()
    {
        Environment.SetEnvironmentVariable("JOB_TEST_KEY", "hello");
        try
        {
            EnvValidator.GetEnv("JOB_TEST_KEY", "fallback").Should().Be("hello");
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOB_TEST_KEY", null);
        }
    }

    [Fact]
    public void GetEnv_Returns_Fallback_When_Unset()
    {
        Environment.SetEnvironmentVariable("JOB_TEST_KEY", null);
        EnvValidator.GetEnv("JOB_TEST_KEY", "fallback").Should().Be("fallback");
    }

    [Fact]
    public void GetEnvInt_Parses_Valid_Integer()
    {
        Environment.SetEnvironmentVariable("JOB_TEST_INT", "42");
        try
        {
            EnvValidator.GetEnvInt("JOB_TEST_INT", 7).Should().Be(42);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOB_TEST_INT", null);
        }
    }

    [Fact]
    public void GetEnvInt_Returns_Fallback_When_Unset()
    {
        Environment.SetEnvironmentVariable("JOB_TEST_INT", null);
        EnvValidator.GetEnvInt("JOB_TEST_INT", 7).Should().Be(7);
    }

    [Fact]
    public void GetEnvInt_Throws_On_Invalid_Integer()
    {
        Environment.SetEnvironmentVariable("JOB_TEST_INT", "not-a-number");
        try
        {
            Action act = () => EnvValidator.GetEnvInt("JOB_TEST_INT", 7);
            act.Should().Throw<ConfigurationError>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOB_TEST_INT", null);
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    public void GetEnvBool_Parses_Valid_Bool(string raw, bool expected)
    {
        Environment.SetEnvironmentVariable("JOB_TEST_BOOL", raw);
        try
        {
            EnvValidator.GetEnvBool("JOB_TEST_BOOL", !expected).Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOB_TEST_BOOL", null);
        }
    }

    [Fact]
    public void GetEnvBool_Returns_Fallback_When_Unset()
    {
        Environment.SetEnvironmentVariable("JOB_TEST_BOOL", null);
        EnvValidator.GetEnvBool("JOB_TEST_BOOL", true).Should().BeTrue();
    }

    [Fact]
    public void GetEnvBool_Throws_On_Invalid_Value()
    {
        Environment.SetEnvironmentVariable("JOB_TEST_BOOL", "maybe");
        try
        {
            Action act = () => EnvValidator.GetEnvBool("JOB_TEST_BOOL", false);
            act.Should().Throw<ConfigurationError>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOB_TEST_BOOL", null);
        }
    }

    [Fact]
    public void Load_Reads_All_Vars_With_Defaults()
    {
        ResetAllVars();
        var s = EnvValidator.Load();
        s.Environment.Should().Be("local");
        s.LogLevel.Should().Be("Information");
        s.ShutdownTimeoutSeconds.Should().Be(30);
        s.JobExecutionTimeoutSeconds.Should().Be(300);
        s.RedisHost.Should().Be("localhost");
        s.RedisPort.Should().Be(6379);
        s.MessagingEnabled.Should().BeFalse();
        s.HealthCheckCron.Should().Be("*/1 * * * *");
        s.HealthCheckEnabled.Should().BeTrue();
    }

    [Fact]
    public void Load_Reads_Overridden_Values()
    {
        Environment.SetEnvironmentVariable("ENVIRONMENT", "prod");
        Environment.SetEnvironmentVariable("JOB_EXECUTION_TIMEOUT_SECONDS", "60");
        Environment.SetEnvironmentVariable("MESSAGING_ENABLED", "true");
        try
        {
            var s = EnvValidator.Load();
            s.Environment.Should().Be("prod");
            s.JobExecutionTimeoutSeconds.Should().Be(60);
            s.MessagingEnabled.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ENVIRONMENT", null);
            Environment.SetEnvironmentVariable("JOB_EXECUTION_TIMEOUT_SECONDS", null);
            Environment.SetEnvironmentVariable("MESSAGING_ENABLED", null);
        }
    }

    private static void ResetAllVars()
    {
        foreach (var key in new[]
        {
            "ENVIRONMENT", "LOG_LEVEL", "SHUTDOWN_TIMEOUT_SECONDS",
            "JOB_EXECUTION_TIMEOUT_SECONDS", "DATABASE_URL",
            "DATABASE_COMMAND_TIMEOUT_SECONDS", "REDIS_HOST", "REDIS_PORT",
            "REDIS_PASSWORD", "REDIS_DB", "REDIS_COMMAND_TIMEOUT_SECONDS",
            "MESSAGING_ENABLED", "RABBIT_URL", "RABBIT_USER", "RABBIT_PASSWORD",
            "RABBITMQ_PUBLISH_TIMEOUT", "HEALTH_CHECK_CRON", "HEALTH_CHECK_ENABLED",
        })
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}
