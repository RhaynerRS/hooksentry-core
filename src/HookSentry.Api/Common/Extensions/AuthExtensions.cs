using System.Text;
using HookSentry.Api.Common.Auth;
using HookSentry.Infrastructure.ApiKeys;
using HookSentry.Infrastructure.Auth;
using HookSentry.Infrastructure.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace HookSentry.Api.Common.Extensions;

public static class AuthExtensions
{
    public const string ApiKeyScheme = "ApiKey";

    public static IServiceCollection AddJwtAndApiKeyAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IApiKeyCacheService, ApiKeyCacheService>();
        services.AddSingleton<IRefreshTokenStore, RedisRefreshTokenStore>();
        services.AddSingleton<IEventIdempotencyStore, RedisEventIdempotencyStore>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
                };
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyScheme, _ => { });

        services.AddAuthorization();

        return services;
    }
}
