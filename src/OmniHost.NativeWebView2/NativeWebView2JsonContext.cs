using System.Text.Json.Serialization;

namespace OmniHost.NativeWebView2;

[JsonSerializable(typeof(BridgeInvokeMessage))]
[JsonSerializable(typeof(BridgeEventMessage))]
[JsonSerializable(typeof(BridgeResponseMessage))]
internal partial class NativeWebView2JsonContext : JsonSerializerContext;

internal sealed record BridgeInvokeMessage(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("handler")] string? Handler,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("data")] string? Data);

internal sealed record BridgeEventMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("data")] string Data);

internal sealed record BridgeResponseMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] string? Result = null,
    [property: JsonPropertyName("error")] string? Error = null);
