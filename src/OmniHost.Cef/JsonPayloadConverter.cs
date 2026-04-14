using System.Text.Json;

namespace OmniHost.Cef;

internal static class JsonPayloadConverter
{
    public static object? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);
        return FromJsonElement(document.RootElement);
    }

    private static object? FromJsonElement(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => FromJsonElement(property.Value),
                    StringComparer.Ordinal),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(FromJsonElement)
                .ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => TryReadNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => null,
        };

    private static object TryReadNumber(JsonElement element)
    {
        if (element.TryGetInt32(out var int32))
            return int32;

        if (element.TryGetInt64(out var int64))
            return int64;

        if (element.TryGetDecimal(out var decimalValue))
            return decimalValue;

        return element.GetDouble();
    }
}
