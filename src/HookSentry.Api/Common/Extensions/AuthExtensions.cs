using System.Text;
using HookSentry.Api.Common.Auth;
using HookSentry.Infrastructure.ApiKeys;
using HookSentry.Infrastructure.Auth;
using HookSentry.Infrastructure.Events;
using HookSentry.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HookSentry.Api.Common.Extensions;

public static class AuthExtensions
{
    public const string ApiKeyScheme = "ApiKey";

    public static IServiceCollection AddJwtAndApiKeyAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IApiKeyCacheService, ApiKeyCacheService>();
        services.AddSingleton<IRefreshTokenStore, RedisRefreshTokenStore>();
        services.AddSingleton<IPublicEndpointRateLimiter, RedisPublicEndpointRateLimiter>();
        services.AddSingleton<IEventIdempotencyStore, RedisEventIdempotencyStore>();
        services.AddSingleton<IJwtDenylist, RedisJwtDenylist>();

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
                        Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async ctx =>
                    {
                        var denylist = ctx.HttpContext.RequestServices.GetRequiredService<IJwtDenylist>();
                        var jti = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                        if (jti is not null && await denylist.IsRevokedAsync(jti))
                            ctx.Fail("Token has been revoked.");
                    },
                    OnChallenge = ctx =>
                    {
                        ctx.HandleResponse();
                        ctx.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }
                };
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyScheme, _ => { });

        services.AddAuthorization();

        return services;
    }
}
