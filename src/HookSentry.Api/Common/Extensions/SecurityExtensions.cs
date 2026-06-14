using HookSentry.Infrastructure.Security;
using HookSentry.Api.Common.Services;
using Microsoft.Extensions.Configuration;

namespace HookSentry.Api.Common.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CredentialEncryptionSettings>(configuration.GetSection("CredentialEncryption"));
        services.AddSingleton<HookSentry.Domain.Security.IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<HookSentry.Domain.Security.ICredentialEncryptionService, AesCredentialEncryptionService>();
        return services;
    }
}
