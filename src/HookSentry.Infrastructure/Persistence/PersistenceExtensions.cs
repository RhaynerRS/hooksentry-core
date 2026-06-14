using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using HookSentry.Domain;
using HookSentry.Domain.ApiKeys;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Events;
using HookSentry.Domain.Invites;
using HookSentry.Domain.Senders;
using HookSentry.Domain.Tenants;
using HookSentry.Domain.Users;
using HookSentry.Infrastructure.Persistence.Mappings;
using HookSentry.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;

namespace HookSentry.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var sessionFactory = Fluently.Configure()
            .Database(PostgreSQLConfiguration.PostgreSQL82
                .ConnectionString(connectionString))
            .Mappings(m => m.FluentMappings
                .AddFromAssemblyOf<TenantMap>()
                .Conventions.Add<DateTimeOffsetConvention>())
            .BuildSessionFactory();

        services.AddSingleton(sessionFactory);
        services.AddScoped(sp => sp.GetRequiredService<ISessionFactory>().OpenSession());

        services.AddScoped<IUnitOfWorkFactory, NHibernateUnitOfWorkFactory>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IDestinationUrlRepository, DestinationUrlRepository>();
        services.AddScoped<IWebhookSenderRepository, WebhookSenderRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IInviteTokenRepository, InviteTokenRepository>();

        return services;
    }
}
