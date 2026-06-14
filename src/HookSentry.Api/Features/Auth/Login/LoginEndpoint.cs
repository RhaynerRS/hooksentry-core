using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.Common.Services;
using HookSentry.Api.Common.Validation;
using HookSentry.Domain.Security;
using HookSentry.Domain.Users;
using HookSentry.Infrastructure.Auth;
using HookSentry.Api.DataTransfer.Auth.Requests;
using HookSentry.Api.DataTransfer.Auth.Responses;

namespace HookSentry.Api.Features.Auth.Login;

public class LoginEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/login", Handle)
            .WithName("Login")
            .WithTags("Auth")
            .WithSummary("Autentica um usuário e retorna tokens de acesso")
            .WithDescription("""
                Autentica um usuário existente e retorna um access token JWT (TTL: 15 minutos) e um refresh token (TTL: 7 dias).
                O refresh token é armazenado no Redis. O JWT é stateless — validado apenas por assinatura e expiração.

                **Body:**
                - `email` *(obrigatório)*: e-mail do usuário
                - `password` *(obrigatório)*: senha do usuário

                **Códigos de retorno:**
                - `200 OK`: autenticação bem-sucedida
                - `400 Bad Request`: campos ausentes
                - `401 Unauthorized`: credenciais incorretas ou usuário inativo
                """)
            .AllowAnonymous()
            .Produces<AuthResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Handle(
        LoginRequest request,
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IPasswordHasher passwordHasher,
        IRefreshTokenStore tokenStore,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest("Email é obrigatório.");
        if (string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest("Senha é obrigatória.");

        if (InputSanitizer.ValidateEmail(request.Email) is { } emailErr)
            return Results.BadRequest(emailErr);

        var user = await userRepository.FindByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Results.Unauthorized();

        if (user.Status != UserStatus.Active)
            return Results.Unauthorized();

        var (accessToken, _, expiresAt) = jwtTokenService.GenerateAccessToken(user);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        await tokenStore.StoreAsync(refreshToken, user.Id, user.TenantId);

        return Results.Ok(new AuthResponse(
            accessToken,
            (int)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds,
            refreshToken,
            expiresAt));
    }
}
