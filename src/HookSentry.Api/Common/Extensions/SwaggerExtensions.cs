using HookSentry.Api.Common.Swagger;
using Microsoft.OpenApi;

namespace HookSentry.Api.Common.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithAuth(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "HookSentry API",
                Version = "v1",
                Description = "Webhook management and security hooks API."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter: Bearer {token}"
            });

            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Name = "X-Api-Key",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "API key for authentication via X-Api-Key header."
            });

            options.OperationFilter<SecurityRequirementsOperationFilter>();
        });

        return services;
    }

    public static WebApplication UseSwaggerWithAuth(this WebApplication app)
    {
        app.UseStaticFiles();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "HookSentry API v1");
            options.RoutePrefix = "swagger";
            options.InjectStylesheet("/swagger/custom.css");
            options.InjectJavascript("/swagger/custom.js");
        });

        return app;
    }
}
