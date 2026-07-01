namespace HookSentry.Api.Common.Extensions;

public static class HttpContextExtensions
{
    public static string GetClientIp(this HttpContext ctx)
    {
        // CF-Connecting-IP é injetado pelo Cloudflare e não pode ser forjado pelo cliente
        // quando a origem só aceita tráfego do Cloudflare (ver spec-cloudflare-protection-13)
        var cfIp = ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cfIp)) return cfIp;

        // Fallback: X-Forwarded-For (primeiro IP = cliente mais próximo)
        var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xff))
            return xff.Split(',')[0].Trim();

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
