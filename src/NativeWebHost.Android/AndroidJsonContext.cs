using System.Text.Json.Serialization;

namespace NativeWebHost.Android;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AndroidBridgeInvokeMessage))]
[JsonSerializable(typeof(AndroidBridgeEventEnvelope))]
[JsonSerializable(typeof(AndroidBridgeResponseEnvelope))]
[JsonSerializable(typeof(AndroidFetchRequest))]
[JsonSerializable(typeof(AndroidFetchResponse))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(string))]
internal sealed partial class AndroidJsonContext : JsonSerializerContext;

internal sealed record AndroidBridgeInvokeMessage(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("handler")] string? Handler,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("data")] string? Data);

internal sealed record AndroidBridgeEventEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("data")] string Data);

internal sealed record AndroidBridgeResponseEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] string? Result = null,
    [property: JsonPropertyName("error")] string? Error = null);

/// <summary>
/// Fetch request sent from the Android JavaScript bridge.
/// </summary>
public sealed record AndroidFetchRequest(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("headers")] IReadOnlyDictionary<string, string> Headers,
    [property: JsonPropertyName("bodyBase64")] string? BodyBase64);

/// <summary>
/// Fetch response returned to the Android JavaScript bridge.
/// </summary>
public sealed record AndroidFetchResponse(
    [property: JsonPropertyName("handled")] bool Handled,
    [property: JsonPropertyName("status")] int Status = 200,
    [property: JsonPropertyName("statusText")] string StatusText = "OK",
    [property: JsonPropertyName("headers")] IReadOnlyDictionary<string, string>? Headers = null,
    [property: JsonPropertyName("body")] string? Body = null,
    [property: JsonPropertyName("bodyBase64")] string? BodyBase64 = null)
{
    public static AndroidFetchResponse Unhandled { get; } = new(false, 404, "Not Found");
}
