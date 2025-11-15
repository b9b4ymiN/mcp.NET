using System.Text.Json.Serialization;

namespace Mcp.SqlApiServer.Models;

/// <summary>
/// Parameters for http.call tool
/// </summary>
public sealed class HttpCallParams
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("query")]
    public Dictionary<string, object>? Query { get; set; }

    [JsonPropertyName("body")]
    public object? Body { get; set; }

    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// Result from http.call tool
/// </summary>
public sealed class HttpCallResult
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [JsonPropertyName("bodyText")]
    public string BodyText { get; set; } = string.Empty;
}
