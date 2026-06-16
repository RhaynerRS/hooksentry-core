using System.Text.Json.Serialization;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Infrastructure.Observability;
using HookSentry.Infrastructure.Persistence;
using HookSentry.Infrastructure.RabbitMq;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

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

var mqConn = app.Services.GetRequiredService<RabbitMqConnection>();
var mqSettings = app.Services.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
await mqConn.ConnectAsync(mqSettings, CancellationToken.None);

app.UseSwaggerWithAuth();
app.UseHttpsRedirection();
app.UseCors(CorsExtensions.SitePolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints();

app.Run();
