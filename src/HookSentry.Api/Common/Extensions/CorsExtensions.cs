namespace HookSentry.Api.Common.Extensions;

public static class CorsExtensions
{
    public const string SitePolicyName = "hooksentry-site";
    public const string OpenPolicyName = "hooksentry-open";

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigin = configuration["Cors:AllowedOrigin"]
            ?? throw new InvalidOperationException("Configuration key 'Cors:AllowedOrigin' is required.");

        services.AddCors(options =>
        {
            options.AddPolicy(SitePolicyName, policy =>
                policy.WithOrigins(allowedOrigin)
                      .AllowAnyHeader()
                      .AllowAnyMethod());

            // Used only on the ingest endpoint — accepts requests from any third-party sender.
            options.AddPolicy(OpenPolicyName, policy =>
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod());
        });

        return services;
    }
}
