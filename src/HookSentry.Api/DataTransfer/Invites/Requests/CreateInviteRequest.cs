using HookSentry.Domain.Users;

namespace HookSentry.Api.DataTransfer.Invites.Requests;

public record CreateInviteRequest(int ValidityDays = 7, UserRole? Role = null);
