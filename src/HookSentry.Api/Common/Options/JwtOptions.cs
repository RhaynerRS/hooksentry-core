using System.ComponentModel.DataAnnotations;

namespace HookSentry.Api.Common.Options;

public sealed class JwtOptions
{
    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    // Minimum 64 ASCII characters ≈ 512 bits; enforces key strength at startup.
    [Required, MinLength(64)]
    public required string Key { get; init; }
}
