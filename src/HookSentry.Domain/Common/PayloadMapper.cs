using System.Text.Json;

namespace HookSentry.Domain.Common;

public static class PayloadMapper
{
    public static string Apply(string mappingJson, string payloadJson)
    {
        var mapping = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(mappingJson)
            ?? throw new ArgumentException("Invalid mapping JSON.", nameof(mappingJson));

        var source = JsonSerializer.Deserialize<JsonElement>(payloadJson);

        var result = new Dictionary<string, object?>();

        foreach (var (outputKey, expression) in mapping)
        {
            var value = ResolveExpression(expression, source);
            if (value is not null)
                result[outputKey] = value;
        }

        return JsonSerializer.Serialize(result);
    }

    private static object? ResolveExpression(JsonElement expression, JsonElement source)
    {
        if (expression.ValueKind == JsonValueKind.Array)
        {
            var items = new List<object?>();
            foreach (var elem in expression.EnumerateArray())
            {
                if (elem.ValueKind == JsonValueKind.String)
                    items.Add(ResolveSingleExpression(elem.GetString()!, source));
            }
            return items;
        }

        if (expression.ValueKind == JsonValueKind.String)
            return ResolveSingleExpression(expression.GetString()!, source);

        return null;
    }

    private static object? ResolveSingleExpression(string expr, JsonElement source)
    {
        var plusIndex = expr.IndexOf('+');
        if (plusIndex > 0)
        {
            var left = ResolveFieldPath(expr[..plusIndex].Trim(), source);
            var right = ResolveFieldPath(expr[(plusIndex + 1)..].Trim(), source);
            return CombineValues(left, right);
        }

        return ResolveFieldPath(expr.Trim(), source);
    }

    private static object? ResolveFieldPath(string path, JsonElement source)
    {
        var current = source;
        foreach (var segment in path.Split(':'))
        {
            if (segment.Contains('['))
            {
                if (!TryResolveBracketSegment(segment, current, out current))
                    return null;
            }
            else
            {
                if (current.ValueKind != JsonValueKind.Object ||
                    !current.TryGetProperty(segment, out current))
                    return null;
            }
        }
        return ExtractValue(current);
    }

    private static bool TryResolveBracketSegment(string segment, JsonElement current, out JsonElement result)
    {
        result = default;
        var bracketIndex = segment.IndexOf('[');
        var closingBracket = segment.IndexOf(']', bracketIndex);
        if (closingBracket < 0) return false;

        var fieldName = segment[..bracketIndex];
        var indexStr = segment[(bracketIndex + 1)..closingBracket];

        if (!string.IsNullOrEmpty(fieldName))
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(fieldName, out current))
                return false;
        }

        if (!int.TryParse(indexStr, out var index) || current.ValueKind != JsonValueKind.Array)
            return false;

        var arr = current.EnumerateArray().ToArray();
        if (index < 0 || index >= arr.Length) return false;

        result = arr[index];
        return true;
    }

    private static object? ExtractValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => null
    };

    private static object? CombineValues(object? left, object? right)
    {
        if (left is null || right is null) return null;

        if (left is long l1 && right is long l2) return l1 + l2;
        if (left is double d1 && right is double d2) return d1 + d2;
        if (left is long lv && right is double dv) return (double)lv + dv;
        if (left is double dv2 && right is long lv2) return dv2 + (double)lv2;

        if (left is string or long or double && right is string or long or double)
            return left.ToString() + right.ToString();

        return null;
    }
}
