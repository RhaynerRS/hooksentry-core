using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HookSentry.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration config)
    {
        var endpoint = config["Otel:Endpoint"]
            ?? throw new InvalidOperationException("Otel:Endpoint configuration is required.");
        var logsEndpoint = config["Otel:LogsEndpoint"] ?? endpoint;
        var serviceName = config["Otel:ServiceName"]
            ?? throw new InvalidOperationException("Otel:ServiceName configuration is required.");

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("HookSentry.Worker")
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(endpoint);
                    o.Protocol = OtlpExportProtocol.Grpc;
                }))
            .WithLogging(logging => logging
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(logsEndpoint);
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                }));

        return services;
    }
}
