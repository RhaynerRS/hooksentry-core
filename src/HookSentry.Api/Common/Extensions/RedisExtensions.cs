using HookSentry.Infrastructure.Destinations;
using StackExchange.Redis;

namespace HookSentry.Api.Common.Extensions;

public static class RedisExtensions
{
    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' not found.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connectionString));

        services.AddSingleton<IDestinationCacheService, DestinationCacheService>();

        return services;
    }
}
