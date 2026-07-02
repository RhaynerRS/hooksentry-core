namespace HookSentry.Api.Common.Extensions;

public static class HttpContextExtensions
{
    public static string GetClientIp(this HttpContext ctx)
    {
        var cfIp = ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cfIp)) return cfIp;

        var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xff))
            return xff.Split(',')[0].Trim();

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
