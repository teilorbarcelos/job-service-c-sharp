using System.Text;
using JobService.Shared.Config;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace JobService.Infrastructure.Messaging;

public sealed class RabbitMqProvider : IRabbitMqProvider
{
    private readonly IConnectionFactory _factory;
    private readonly ILogger<RabbitMqProvider> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();
    private bool _disposed;

    public RabbitMqProvider(IOptions<AppSettings> options, ILogger<RabbitMqProvider> logger)
        : this(BuildFactory(options.Value), logger)
    {
    }

    internal RabbitMqProvider(IConnectionFactory factory, ILogger<RabbitMqProvider> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static IConnectionFactory BuildFactory(AppSettings cfg)
    {
        var uri = new Uri(cfg.RabbitUrl);
        return new ConnectionFactory
        {
            Uri = uri,
            UserName = string.IsNullOrEmpty(cfg.RabbitUser) ? uri.UserInfo.Split(':')[0] : cfg.RabbitUser,
            Password = string.IsNullOrEmpty(cfg.RabbitPassword)
                ? (uri.UserInfo.Contains(':') ? uri.UserInfo.Split(':')[1] : string.Empty)
                : cfg.RabbitPassword,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        };
    }

    public void Connect()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqProvider));
        lock (_lock)
        {
            if (_connection?.IsOpen == true) return;
            _connection = _factory.CreateConnection("job-service");
            _channel = _connection.CreateModel();
            _logger.LogInformation("RabbitMQ connected");
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            try { _channel?.Close(); } catch { }
            try { _connection?.Close(); } catch { }
            _channel = null;
            _connection = null;
        }
    }

    public bool Check()
    {
        return _connection?.IsOpen == true && _channel?.IsOpen == true;
    }

    public void Publish(string exchange, string routingKey, ReadOnlyMemory<byte> body)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqProvider));
        if (!Check())
            throw new InvalidOperationException("RabbitMQ is not connected");
        lock (_lock)
        {
            var props = _channel!.CreateBasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = 2;
            _channel.BasicPublish(exchange, routingKey, props, body);
        }
    }

    public void PublishJson(string exchange, string routingKey, string json)
        => Publish(exchange, routingKey, Encoding.UTF8.GetBytes(json));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}
