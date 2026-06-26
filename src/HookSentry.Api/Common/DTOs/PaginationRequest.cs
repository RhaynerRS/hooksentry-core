namespace HookSentry.Api.Common.DTOs;

public enum SortOrder { Asc, Desc }

public class PaginationRequest
{
    public int Qt { get; set; } = 10;
    public int Pg { get; set; } = 1;
    public string CpOrd { get; set; } = "id";
    public SortOrder TpOrd { get; set; } = SortOrder.Desc;

    public PaginationRequest() { }
}
