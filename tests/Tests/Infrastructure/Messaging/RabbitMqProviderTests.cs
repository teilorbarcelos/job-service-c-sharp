using FluentAssertions;
using JobService.Infrastructure.Messaging;
using JobService.Shared.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace JobService.Tests.Infrastructure.Messaging;

public class RabbitMqProviderTests
{
    [Fact]
    public void Ctor_Throws_On_Null_Factory()
    {
        Action act = () => new RabbitMqProvider((IConnectionFactory)null!, NullLogger<RabbitMqProvider>.Instance);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_Throws_On_Null_Logger()
    {
        Action act = () => new RabbitMqProvider(Mock.Of<IConnectionFactory>(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Check_Returns_False_When_Not_Connected()
    {
        var provider = new RabbitMqProvider(
            Mock.Of<IConnectionFactory>(),
            NullLogger<RabbitMqProvider>.Instance);

        provider.Check().Should().BeFalse();
    }

    [Fact]
    public void PublishJson_Throws_When_Not_Connected()
    {
        var provider = new RabbitMqProvider(
            Mock.Of<IConnectionFactory>(),
            NullLogger<RabbitMqProvider>.Instance);

        Action act = () => provider.PublishJson("ex", "rk", "{}");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Ctor_With_AppSettings_Builds_Factory_From_Url()
    {
        var settings = Options.Create(new AppSettings
        {
            RabbitUrl = BuildAmqpHost("h", 5672),
            RabbitUser = "u",
            RabbitPassword = "p",
        });
        var provider = new RabbitMqProvider(settings, NullLogger<RabbitMqProvider>.Instance);
        provider.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_With_AppSettings_Falls_Back_To_Url_Default_User()
    {
        var settings = Options.Create(new AppSettings
        {
            RabbitUrl = BuildAmqpHost("h", 5672),
            RabbitUser = "",
            RabbitPassword = "",
        });
        var provider = new RabbitMqProvider(settings, NullLogger<RabbitMqProvider>.Instance);
        provider.Should().NotBeNull();
    }

    private static string BuildAmqpHost(string host, int port)
        => $"amqp://{host}:{port}/";

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times()
    {
        var provider = new RabbitMqProvider(
            Mock.Of<IConnectionFactory>(),
            NullLogger<RabbitMqProvider>.Instance);

        provider.Dispose();
        provider.Dispose();
    }

    [Fact]
    public void Connect_Creates_Connection_And_Channel()
    {
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.IsOpen).Returns(true);
        var channel = new Mock<IModel>();
        channel.Setup(c => c.IsOpen).Returns(true);
        connection.Setup(c => c.CreateModel()).Returns(channel.Object);

        var factory = new Mock<IConnectionFactory>();
        factory.Setup(f => f.CreateConnection(It.IsAny<string>())).Returns(connection.Object);

        var provider = new RabbitMqProvider(factory.Object, NullLogger<RabbitMqProvider>.Instance);
        provider.Connect();

        provider.Check().Should().BeTrue();
        connection.Verify(c => c.CreateModel(), Times.Once);
    }

    [Fact]
    public void Connect_Is_Idempotent_When_Already_Open()
    {
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.IsOpen).Returns(true);
        var channel = new Mock<IModel>();
        channel.Setup(c => c.IsOpen).Returns(true);
        connection.Setup(c => c.CreateModel()).Returns(channel.Object);

        var factory = new Mock<IConnectionFactory>();
        factory.Setup(f => f.CreateConnection(It.IsAny<string>())).Returns(connection.Object);

        var provider = new RabbitMqProvider(factory.Object, NullLogger<RabbitMqProvider>.Instance);
        provider.Connect();
        provider.Connect();

        factory.Verify(f => f.CreateConnection(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Disconnect_Closes_And_Nulls_Connection()
    {
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.IsOpen).Returns(true);
        var channel = new Mock<IModel>();
        channel.Setup(c => c.IsOpen).Returns(true);
        connection.Setup(c => c.CreateModel()).Returns(channel.Object);

        var factory = new Mock<IConnectionFactory>();
        factory.Setup(f => f.CreateConnection(It.IsAny<string>())).Returns(connection.Object);

        var provider = new RabbitMqProvider(factory.Object, NullLogger<RabbitMqProvider>.Instance);
        provider.Connect();
        provider.Disconnect();

        channel.Verify(c => c.Close(), Times.Once);
        connection.Verify(c => c.Close(), Times.Once);
        provider.Check().Should().BeFalse();
    }

    [Fact]
    public void Disconnect_Handles_Null_Channel_And_Connection()
    {
        var provider = new RabbitMqProvider(
            Mock.Of<IConnectionFactory>(),
            NullLogger<RabbitMqProvider>.Instance);
        provider.Disconnect();
    }

    [Fact]
    public void Publish_Writes_To_Channel_When_Connected()
    {
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.IsOpen).Returns(true);
        var channel = new Mock<IModel>();
        channel.Setup(c => c.IsOpen).Returns(true);
        connection.Setup(c => c.CreateModel()).Returns(channel.Object);
        var props = new Mock<IBasicProperties>();
        channel.Setup(c => c.CreateBasicProperties()).Returns(props.Object);

        var factory = new Mock<IConnectionFactory>();
        factory.Setup(f => f.CreateConnection(It.IsAny<string>())).Returns(connection.Object);

        var provider = new RabbitMqProvider(factory.Object, NullLogger<RabbitMqProvider>.Instance);
        provider.Connect();
        provider.Publish("ex", "rk", new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 }));

        channel.Verify(c => c.BasicPublish(
            "ex", "rk", It.IsAny<bool>(),
            It.IsAny<IBasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [Fact]
    public void PublishJson_Encodes_And_Publishes()
    {
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.IsOpen).Returns(true);
        var channel = new Mock<IModel>();
        channel.Setup(c => c.IsOpen).Returns(true);
        connection.Setup(c => c.CreateModel()).Returns(channel.Object);
        var props = new Mock<IBasicProperties>();
        channel.Setup(c => c.CreateBasicProperties()).Returns(props.Object);

        var factory = new Mock<IConnectionFactory>();
        factory.Setup(f => f.CreateConnection(It.IsAny<string>())).Returns(connection.Object);

        var provider = new RabbitMqProvider(factory.Object, NullLogger<RabbitMqProvider>.Instance);
        provider.Connect();
        provider.PublishJson("ex", "rk", "{\"k\":1}");

        channel.Verify(c => c.BasicPublish(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<IBasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [Fact]
    public void Check_Returns_False_When_Channel_Not_Open()
    {
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.IsOpen).Returns(true);
        var channel = new Mock<IModel>();
        channel.Setup(c => c.IsOpen).Returns(false);
        connection.Setup(c => c.CreateModel()).Returns(channel.Object);

        var factory = new Mock<IConnectionFactory>();
        factory.Setup(f => f.CreateConnection(It.IsAny<string>())).Returns(connection.Object);

        var provider = new RabbitMqProvider(factory.Object, NullLogger<RabbitMqProvider>.Instance);
        provider.Connect();

        provider.Check().Should().BeFalse();
    }

    [Fact]
    public void Publish_Throws_After_Dispose()
    {
        var provider = new RabbitMqProvider(
            Mock.Of<IConnectionFactory>(),
            NullLogger<RabbitMqProvider>.Instance);
        provider.Dispose();

        Action act = () => provider.Publish("ex", "rk", new ReadOnlyMemory<byte>(new byte[] { 1 }));
        act.Should().Throw<ObjectDisposedException>();
    }
}
