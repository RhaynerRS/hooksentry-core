using HookSentry.Api.Common.DTOs;

namespace HookSentry.Api.DataTransfer.ApiKeys.Requests;

public class GetApiKeysRequest : PaginationRequest
{
public bool? IsActive { get; set; }
}
