using HookSentry.Api.Common.DTOs;

namespace HookSentry.Api.DataTransfer.Events.Requests;

public class GetEventsRequest : PaginationRequest
{
public string? Status { get; set; }
    public Guid? DestinationUrlId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}
