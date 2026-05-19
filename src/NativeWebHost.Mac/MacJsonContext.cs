using System.Text.Json.Serialization;

namespace NativeWebHost.Mac;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WKBridgeInvokeMessage))]
[JsonSerializable(typeof(WKBridgeEventEnvelope))]
[JsonSerializable(typeof(WKBridgeResponseEnvelope))]
[JsonSerializable(typeof(MacWindowLifecyclePayload))]
[JsonSerializable(typeof(string))]
internal sealed partial class MacJsonContext : JsonSerializerContext;

internal sealed record WKBridgeInvokeMessage(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("handler")] string? Handler,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("data")] string? Data);

internal sealed record WKBridgeEventEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("data")] string Data);

internal sealed record WKBridgeResponseEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] string? Result = null,
    [property: JsonPropertyName("error")] string? Error = null);

internal sealed record MacWindowLifecyclePayload(
    [property: JsonPropertyName("windowId")] string WindowId,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("isMinimized")] bool? IsMinimized = null,
    [property: JsonPropertyName("isMaximized")] bool? IsMaximized = null,
    [property: JsonPropertyName("width")] int? Width = null,
    [property: JsonPropertyName("height")] int? Height = null);
