using System.Text.Json;
using HookSentry.Api.Common.Validation;
using HookSentry.Domain.Destinations;

namespace HookSentry.Api.Features.Destinations;

internal static class CredentialValidator
{
    private const int MaxHeaderNameLength = 100;
    private const int MaxHeaderValueLength = 2048;
    private const int MaxTokenLength = 4096;
    private const int MaxIdentifierLength = 512;
    private const int MaxUrlLength = 2048;
    private const int MaxScopeLength = 512;

    public static string? Validate(DestinationAuthType authType, JsonElement credentials) =>
        authType switch
        {
            DestinationAuthType.ApiKey      => ValidateApiKey(credentials),
            DestinationAuthType.BearerToken => ValidateBearerToken(credentials),
            DestinationAuthType.JwtBearer   => ValidateJwtBearer(credentials),
            DestinationAuthType.BasicAuth   => ValidateBasicAuth(credentials),
            _ => $"AuthType '{authType}' is not supported."
        };

    private static string? ValidateApiKey(JsonElement json)
    {
        var err = RequireStrings(json, "headerName", "value");
        if (err is not null) return err;

        var headerName = json.GetProperty("headerName").GetString()!;
        if (headerName.Length > MaxHeaderNameLength)
            return $"'headerName' cannot exceed {MaxHeaderNameLength} characters.";
        if (!InputSanitizer.IsValidHttpHeaderName(headerName))
            return "'headerName' contains invalid characters. Use only letters, digits and hyphens.";

        var value = json.GetProperty("value").GetString()!;
        if (value.Length > MaxHeaderValueLength)
            return $"'value' cannot exceed {MaxHeaderValueLength} characters.";
        if (InputSanitizer.HasControlChars(value))
            return "'value' cannot contain control characters (\\r, \\n, \\0).";

        return null;
    }

    private static string? ValidateBearerToken(JsonElement json)
    {
        var err = RequireStrings(json, "token");
        if (err is not null) return err;

        var token = json.GetProperty("token").GetString()!;
        if (token.Length > MaxTokenLength)
            return $"'token' cannot exceed {MaxTokenLength} characters.";
        if (InputSanitizer.HasControlChars(token))
            return "'token' cannot contain control characters (\\r, \\n, \\0).";

        return null;
    }

    private static string? ValidateJwtBearer(JsonElement json)
    {
        var err = RequireStrings(json, "tokenEndpoint", "clientId", "clientSecret");
        if (err is not null) return err;

        var tokenEndpoint = json.GetProperty("tokenEndpoint").GetString()!;
        var urlErr = ValidateHttpsUrl(tokenEndpoint, MaxUrlLength, "tokenEndpoint");
        if (urlErr is not null) return urlErr;

        var clientId = json.GetProperty("clientId").GetString()!;
        if (clientId.Length > MaxIdentifierLength)
            return $"'clientId' cannot exceed {MaxIdentifierLength} characters.";
        if (InputSanitizer.HasControlChars(clientId))
            return "'clientId' cannot contain control characters.";

        var clientSecret = json.GetProperty("clientSecret").GetString()!;
        if (clientSecret.Length > MaxIdentifierLength)
            return $"'clientSecret' cannot exceed {MaxIdentifierLength} characters.";
        if (InputSanitizer.HasControlChars(clientSecret))
            return "'clientSecret' cannot contain control characters.";

        if (json.TryGetProperty("scope", out var scopeProp) &&
            scopeProp.ValueKind == JsonValueKind.String)
        {
            var scope = scopeProp.GetString()!;
            if (scope.Length > MaxScopeLength)
                return $"'scope' cannot exceed {MaxScopeLength} characters.";
            if (InputSanitizer.HasControlChars(scope))
                return "'scope' cannot contain control characters.";
        }

        return null;
    }

    private static string? ValidateBasicAuth(JsonElement json)
    {
        var err = RequireStrings(json, "username", "password");
        if (err is not null) return err;

        var username = json.GetProperty("username").GetString()!;
        if (username.Length > MaxIdentifierLength)
            return $"'username' cannot exceed {MaxIdentifierLength} characters.";
        if (InputSanitizer.HasControlChars(username))
            return "'username' cannot contain control characters.";
        if (username.Contains(':'))
            return "'username' cannot contain ':' (RFC 7617 — Basic Auth does not support colons in username).";

        var password = json.GetProperty("password").GetString()!;
        if (password.Length > MaxIdentifierLength)
            return $"'password' cannot exceed {MaxIdentifierLength} characters.";
        if (InputSanitizer.HasControlChars(password))
            return "'password' cannot contain control characters.";

        return null;
    }

    private static string? RequireStrings(JsonElement json, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!json.TryGetProperty(field, out var prop) ||
                prop.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(prop.GetString()))
                return $"Field '{field}' is required and cannot be empty.";
        }
        return null;
    }

    private static string? ValidateHttpsUrl(string url, int maxLength, string fieldName)
    {
        if (url.Length > maxLength)
            return $"'{fieldName}' cannot exceed {maxLength} characters.";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return $"'{fieldName}' must be a valid HTTPS URL (SSRF: non-HTTPS URLs are not accepted).";
        return null;
    }
}
