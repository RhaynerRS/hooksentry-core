namespace HookSentry.Infrastructure.AbuseProtection;

public sealed class RegistrationAbuseOptions
{
    public bool FingerprintEnabled           { get; set; } = false;
    public int  MaxAccountsPerFingerprint    { get; set; } = 3;
    public int  FingerprintBlockWindowDays   { get; set; } = 30;

    public bool RegistrationRateLimitEnabled { get; set; } = false;
    public int  MaxRegistrationsPerIpPerHour { get; set; } = 3;

    public bool DisposableEmailBlockEnabled  { get; set; } = false;
}
