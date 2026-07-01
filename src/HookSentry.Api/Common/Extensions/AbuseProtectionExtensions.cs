using HookSentry.Infrastructure.AbuseProtection;

namespace HookSentry.Api.Common.Extensions;

public static class AbuseProtectionExtensions
{
    public static IServiceCollection AddAbuseProtection(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<RegistrationAbuseOptions>(
            config.GetSection("CloudProtection:RegistrationAbuse"));

        services.AddSingleton<IFingerprintGuard, RedisFingerprintGuard>();
        services.AddSingleton<IDisposableEmailChecker, FileBasedDisposableEmailChecker>();

        return services;
    }
}
