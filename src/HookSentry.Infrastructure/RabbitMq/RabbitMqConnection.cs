using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace HookSentry.Infrastructure.RabbitMq;

public sealed class RabbitMqConnection : IAsyncDisposable
{
    private IConnection? _connection;

    public async Task ConnectAsync(RabbitMqSettings settings, CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.Username,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost
        };

        var delays = new[] { 2, 4, 8, 16, 32 };
        foreach (var delay in delays)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync(ct);
                return;
            }
            catch (BrokerUnreachableException) when (delay != delays[^1])
            {
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
            }
        }

        _connection = await factory.CreateConnectionAsync(ct);
    }

    public IConnection GetConnection()
    {
        if (_connection is null)
            throw new InvalidOperationException("Call ConnectAsync before GetConnection.");

        return _connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
