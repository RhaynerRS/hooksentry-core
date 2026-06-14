using HookSentry.Domain.Users;

namespace HookSentry.Api.Common.Services;

public interface IJwtTokenService
{
    (string token, string jti, DateTimeOffset expiresAt) GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
