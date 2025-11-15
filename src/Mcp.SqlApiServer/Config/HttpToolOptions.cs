namespace Mcp.SqlApiServer.Config;

/// <summary>
/// Configuration options for HTTP tool
/// </summary>
public sealed class HttpToolOptions
{
    public const string SectionName = "HttpTool";

    /// <summary>
    /// List of allowed hostnames (e.g., "api.github.com", "example.com")
    /// Requests to other hosts will be rejected
    /// </summary>
    public List<string> AllowedHosts { get; set; } = new();

    /// <summary>
    /// Default timeout in seconds for HTTP requests
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum timeout in seconds that can be requested
    /// </summary>
    public int MaxTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Whether to allow all hosts (WARNING: security risk, use only for development)
    /// </summary>
    public bool AllowAllHosts { get; set; } = false;
}
