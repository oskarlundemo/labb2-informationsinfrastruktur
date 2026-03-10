using System.Globalization;
using System.Text.Json;

namespace PlacementService.Api.Services;

public static class JsonHelpers
{
    public static string? GetString(JsonElement element, params string[] path)
    {
        if (!TryGetElement(element, path, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var number) ? number.ToString(CultureInfo.InvariantCulture) : value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public static DateTimeOffset? GetDateTimeOffset(JsonElement element, params string[] path)
    {
        var text = GetString(element, path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public static bool TryGetElement(JsonElement element, string[] path, out JsonElement value)
    {
        value = element;
        foreach (var segment in path)
        {
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(segment, out var fromObject))
            {
                value = fromObject;
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                if (int.TryParse(segment, out var index) && index >= 0 && index < value.GetArrayLength())
                {
                    value = value[index];
                    continue;
                }

                var matched = false;
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(segment, out var fromArrayObject))
                    {
                        value = fromArrayObject;
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    continue;
                }
            }

            value = default;
            return false;
        }

        return true;
    }
}
