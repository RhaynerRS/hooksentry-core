namespace HookSentry.Api.Common.Validation;

internal static class InputSanitizer
{
    private const int MaxRefreshTokenLength = 512;
    private const int MaxEmailLength = 255;
    private const int MaxNameLength = 255;

    public static bool HasControlChars(string value) =>
        value.Any(c => c is '\r' or '\n' or '\0');

    public static bool IsValidHttpHeaderName(string name) =>
        name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static bool IsValidHttpsUrl(string url, int maxLength, out string? error)
    {
        if (url.Length > maxLength)
        {
            error = $"URL cannot exceed {maxLength} characters.";
            return false;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            error = "URL must be a valid HTTPS URL.";
            return false;
        }
        error = null;
        return true;
    }

    public static string? ValidateEmail(string email)
    {
        if (email.Length > MaxEmailLength)
            return $"'email' cannot exceed {MaxEmailLength} characters.";
        if (HasControlChars(email))
            return "'email' cannot contain control characters (\\r, \\n, \\0).";
        return null;
    }

    public static string? ValidateName(string name)
    {
        if (name.Length > MaxNameLength)
            return $"'name' cannot exceed {MaxNameLength} characters.";
        if (HasControlChars(name))
            return "'name' cannot contain control characters (\\r, \\n, \\0).";
        return null;
    }

    public static string? ValidateToken(string token)
    {
        if (token.Length > MaxRefreshTokenLength)
            return $"Token cannot exceed {MaxRefreshTokenLength} characters.";
        if (HasControlChars(token))
            return "Token cannot contain control characters (\\r, \\n, \\0).";
        return null;
    }
}
