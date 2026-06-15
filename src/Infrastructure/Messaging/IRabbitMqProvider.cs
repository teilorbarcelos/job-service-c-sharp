namespace JobService.Infrastructure.Messaging;

public interface IRabbitMqProvider : IDisposable
{
    void Connect();
    void Disconnect();
    bool Check();
    void Publish(string exchange, string routingKey, ReadOnlyMemory<byte> body);
    void PublishJson(string exchange, string routingKey, string json);
}
