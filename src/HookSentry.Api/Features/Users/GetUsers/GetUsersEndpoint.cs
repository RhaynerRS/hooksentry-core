using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using HookSentry.Api.Common.DTOs;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Users.Requests;
using HookSentry.Api.DataTransfer.Users.Responses;
using HookSentry.Domain.Users;
using NHibernate.Linq;

namespace HookSentry.Api.Features.Users.GetUsers;

public class GetUsersEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users", Handle)
            .WithName("GetUsers")
            .WithTags("Users")
            .WithSummary("Lista usuários do tenant autenticado com paginação")
            .WithDescription("""
                Retorna uma página de usuários pertencentes ao tenant autenticado (RNF-007).
                A listagem é sempre isolada por tenant — nunca expõe usuários de outros tenants.

                **Paginação** *(todos opcionais — possuem valores padrão):*
                - `Qt`: itens por página (padrão: `10`)
                - `Pg`: número da página, base 1 (padrão: `1`)
                - `CpOrd`: campo de ordenação, case-insensitive (padrão: `id`)
                - `TpOrd`: direção — `0` = Asc, `1` = Desc (padrão: `1`)

                **Filtros** *(todos opcionais):*
                - `Status`: filtra por status — `0` = Active, `1` = Inactive
                - `Role`: filtra por perfil — `0` = Developer, `1` = Admin

                **Códigos de retorno:**
                - `200 OK`: lista paginada de usuários (sem campo password)
                - `400 Bad Request`: campo de ordenação inválido
                - `401 Unauthorized`: token ausente ou inválido
                """)
            .RequireAuthorization()
            .Produces<PaginationResponse<UserResponse>>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetUsersRequest request,
        ClaimsPrincipal principal,
        IUserRepository userRepository,
        CancellationToken ct)
    {
        if (principal.RequireTenantId(out var tenantId) is { } err) return err;

        var query = userRepository.Query().Where(u => u.TenantId == tenantId);

        if (request.Status.HasValue)
            query = query.Where(u => u.Status == request.Status.Value);

        if (request.Role.HasValue)
            query = query.Where(u => u.Role == request.Role.Value);

        var total = await query.CountAsync(ct);

        List<User> items;
        try
        {
            items = await ApplyOrdering(query, request)
                .Skip((request.Pg - 1) * request.Qt)
                .Take(request.Qt)
                .ToListAsync(ct);
        }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        return Results.Ok(new PaginationResponse<UserResponse>(
            total,
            [..items.Select(UserResponse.From)]));
    }

    private static IQueryable<User> ApplyOrdering(IQueryable<User> query, PaginationRequest request)
    {
        var prop = typeof(User).GetProperty(
            request.CpOrd,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException($"'{request.CpOrd}' is not a valid sort column.");

        var param = Expression.Parameter(typeof(User), "u");
        var keySelector = Expression.Lambda<Func<User, object>>(
            Expression.Convert(Expression.Property(param, prop), typeof(object)), param);

        return request.TpOrd == SortOrder.Asc
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}
