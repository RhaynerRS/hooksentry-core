using HookSentry.Infrastructure.Observability;
using HookSentry.Infrastructure.Persistence;
using HookSentry.Infrastructure.RabbitMq;
using HookSentry.Infrastructure.Security;
using HookSentry.Worker.Consumers;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddObservability(builder.Configuration);
builder.Services.Configure<CredentialEncryptionSettings>(
    builder.Configuration.GetSection("CredentialEncryption"));
builder.Services.AddSingleton<HookSentry.Domain.Security.ICredentialEncryptionService, AesCredentialEncryptionService>();
builder.Services.AddHostedService<WebhookDeliveryConsumer>();

var host = builder.Build();

var mqConnection = host.Services.GetRequiredService<RabbitMqConnection>();
var settings = host.Services.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
await mqConnection.ConnectAsync(settings, CancellationToken.None);

await host.RunAsync();
