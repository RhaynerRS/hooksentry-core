using HookSentry.Api.Common.DTOs;
using HookSentry.Domain.Users;

namespace HookSentry.Api.DataTransfer.Users.Requests;

public class GetUsersRequest : PaginationRequest
{
public UserStatus? Status { get; set; }
    public UserRole? Role { get; set; }
}
