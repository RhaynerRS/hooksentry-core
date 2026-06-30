using HookSentry.Domain.Invites;
using HookSentry.Domain.Users;

namespace HookSentry.Api.DataTransfer.Invites.Responses;

public record InviteTokenResponse(
    Guid Id,
    Guid TenantId,
    string Token,
    UserRole TargetRole,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? UsedAt,
    InviteTokenStatus Status,
    DateTimeOffset CreatedAt)
{
    public static InviteTokenResponse From(InviteToken t) =>
        new(t.Id, t.TenantId, t.Token, t.TargetRole, t.ExpiresAt, t.UsedAt, t.Status, t.CreatedAt);
}
