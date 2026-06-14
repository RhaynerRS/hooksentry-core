using HookSentry.Api.Common.Endpoints;

namespace HookSentry.Api.Features.Health;

public class HealthCheckEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/health", Handle)
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Checks API health")
            .WithDescription("""
                Public health check endpoint. Returns API status and current UTC timestamp.

                **No authentication required.**
                """)
            .AllowAnonymous()
            .Produces<HealthResponse>();
    }

    private static IResult Handle() =>
        Results.Ok(new HealthResponse("healthy", DateTime.UtcNow));
}

public record HealthResponse(string Status, DateTime Timestamp);
