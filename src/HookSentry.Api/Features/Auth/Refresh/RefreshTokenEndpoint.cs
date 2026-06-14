using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Services;
using HookSentry.Api.Common.Validation;
using HookSentry.Domain.Users;
using HookSentry.Infrastructure.Auth;
using HookSentry.Api.DataTransfer.Auth.Requests;
using HookSentry.Api.DataTransfer.Auth.Responses;

namespace HookSentry.Api.Features.Auth.Refresh;

public class RefreshTokenEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/refresh", Handle)
            .WithName("RefreshToken")
            .WithTags("Auth")
            .WithSummary("Renova o access token usando um refresh token válido")
            .WithDescription("""
                Troca um refresh token ativo por um novo par de tokens (access token + refresh token).
                O refresh token informado é consumido atomicamente (single-use via Redis GETDEL).

                **Body:**
                - `refreshToken` *(obrigatório)*: refresh token obtido no login ou em refresh anterior

                **Códigos de retorno:**
                - `200 OK`: novos tokens emitidos com sucesso
                - `400 Bad Request`: campo obrigatório ausente
                - `401 Unauthorized`: refresh token inválido, expirado ou já utilizado
                """)
            .AllowAnonymous()
            .Produces<AuthResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Handle(
        RefreshTokenRequest request,
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IRefreshTokenStore tokenStore,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Results.BadRequest("RefreshToken é obrigatório.");
        if (InputSanitizer.ValidateToken(request.RefreshToken) is { } tokenErr)
            return Results.BadRequest(tokenErr);

        var stored = await tokenStore.ConsumeAsync(request.RefreshToken);
        if (stored is null)
            return Results.Unauthorized();

        var (userId, tenantId) = stored.Value;
        var user = await userRepository.FindAsync(userId, ct);
        if (user is null || user.Status != UserStatus.Active || user.TenantId != tenantId)
            return Results.Unauthorized();

        var (accessToken, _, expiresAt) = jwtTokenService.GenerateAccessToken(user);
        var newRefreshToken = jwtTokenService.GenerateRefreshToken();

        await tokenStore.StoreAsync(newRefreshToken, user.Id, user.TenantId);

        return Results.Ok(new AuthResponse(
            accessToken,
            (int)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds,
            newRefreshToken,
            expiresAt));
    }
}
