using FluentAssertions;
using JobService.Shared.Errors;
using Xunit;

namespace JobService.Tests.Shared.Errors;

public class AppErrorTests
{
    [Fact]
    public void AppError_Stores_Code_Message_And_Status()
    {
        var error = new AppError("CUSTOM", "something broke", 418);

        error.Code.Should().Be("CUSTOM");
        error.Message.Should().Be("something broke");
        error.StatusCode.Should().Be(418);
    }

    [Fact]
    public void ConfigurationError_Has_Default_Code_And_500()
    {
        var error = new ConfigurationError("missing env");

        error.Code.Should().Be("CONFIGURATION_ERROR");
        error.StatusCode.Should().Be(500);
        error.Message.Should().Be("missing env");
    }

    [Fact]
    public void ValidationError_Has_400_Status()
    {
        var error = new ValidationError("bad input");

        error.Code.Should().Be("VALIDATION_ERROR");
        error.StatusCode.Should().Be(400);
    }

    [Fact]
    public void ConnectionError_Prefixes_Service_Name()
    {
        var error = new ConnectionError("RabbitMQ", "timeout");

        error.Code.Should().Be("CONNECTION_ERROR");
        error.StatusCode.Should().Be(503);
        error.Message.Should().Be("RabbitMQ: timeout");
    }
}
