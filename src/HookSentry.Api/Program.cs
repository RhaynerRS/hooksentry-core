using System.Text.Json.Serialization;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Infrastructure.Observability;
using HookSentry.Infrastructure.Persistence;
using HookSentry.Infrastructure.RabbitMq;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key não configurado. Defina a variável de ambiente Jwt__Key ou a chave em appsettings.");

if (System.Text.Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException(
        "Jwt:Key deve ter pelo menos 32 bytes (256 bits). " +
        "Gere uma chave segura: openssl rand -base64 64");

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddEndpoints()
    .AddPersistence(builder.Configuration)
    .AddRedis(builder.Configuration)
    .AddSecurity(builder.Configuration)
    .AddJwtAndApiKeyAuth(builder.Configuration)
    .AddCorsPolicy(builder.Configuration)
    .AddSwaggerWithAuth()
    .AddRabbitMq(builder.Configuration)
    .AddObservability(builder.Configuration);

var app = builder.Build();

app.Services.MigrateDatabase(app.Configuration);

var mqConn = app.Services.GetRequiredService<RabbitMqConnection>();
var mqSettings = app.Services.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
await mqConn.ConnectAsync(mqSettings, CancellationToken.None);

app.UseSwaggerWithAuth();
app.UseCors(CorsExtensions.SitePolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints();

app.Run();
