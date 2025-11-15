using System.Text;
using System.Text.Json;
using System.Web;
using Mcp.SqlApiServer.Config;
using Mcp.SqlApiServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mcp.SqlApiServer.Tools;

/// <summary>
/// Service for executing HTTP API calls
/// </summary>
public sealed class HttpToolService
{
    private readonly HttpClient _httpClient;
    private readonly HttpToolOptions _options;
    private readonly ILogger<HttpToolService> _logger;

    public HttpToolService(
        IHttpClientFactory httpClientFactory,
        IOptions<HttpToolOptions> options,
        ILogger<HttpToolService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("McpHttpClient");
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Execute an HTTP call
    /// </summary>
    public async Task<HttpCallResult> CallAsync(HttpCallParams parameters, CancellationToken cancellationToken = default)
    {
        // Validate URL
        if (string.IsNullOrWhiteSpace(parameters.Url))
        {
            throw new ArgumentException("URL is required", nameof(parameters));
        }

        if (!Uri.TryCreate(parameters.Url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid URL: {parameters.Url}", nameof(parameters));
        }

        // Check allowed hosts
        if (!_options.AllowAllHosts && !IsHostAllowed(uri.Host))
        {
            throw new InvalidOperationException(
                $"Host '{uri.Host}' is not in the allowed hosts list. " +
                $"Allowed hosts: {string.Join(", ", _options.AllowedHosts)}");
        }

        // Build URL with query parameters
        var finalUrl = BuildUrlWithQuery(parameters.Url, parameters.Query);

        // Validate HTTP method
        var method = new HttpMethod(parameters.Method.ToUpperInvariant());

        // Create request
        using var request = new HttpRequestMessage(method, finalUrl);

        // Add headers
        if (parameters.Headers != null)
        {
            foreach (var (key, value) in parameters.Headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        // Add body for non-GET requests
        if (parameters.Body != null && method != HttpMethod.Get && method != HttpMethod.Head)
        {
            var json = JsonSerializer.Serialize(parameters.Body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        // Set timeout
        var timeout = GetEffectiveTimeout(parameters.TimeoutSeconds);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        _logger.LogInformation("Executing HTTP {Method} to {Url}", method, finalUrl);

        try
        {
            // Execute request
            using var response = await _httpClient.SendAsync(request, cts.Token);

            // Read response
            var bodyText = await response.Content.ReadAsStringAsync(cts.Token);

            // Extract response headers
            var responseHeaders = new Dictionary<string, string>();
            foreach (var header in response.Headers)
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }
            foreach (var header in response.Content.Headers)
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }

            var result = new HttpCallResult
            {
                StatusCode = (int)response.StatusCode,
                Headers = responseHeaders,
                BodyText = bodyText
            };

            _logger.LogInformation(
                "HTTP request completed with status {StatusCode}, body length: {Length}",
                result.StatusCode,
                bodyText.Length);

            return result;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("HTTP request timed out after {Timeout} seconds", timeout);
            throw new TimeoutException($"HTTP request timed out after {timeout} seconds");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed: {Message}", ex.Message);
            throw new InvalidOperationException($"HTTP request failed: {ex.Message}", ex);
        }
    }

    private bool IsHostAllowed(string host)
    {
        return _options.AllowedHosts.Any(allowed =>
            string.Equals(allowed, host, StringComparison.OrdinalIgnoreCase));
    }

    private string BuildUrlWithQuery(string baseUrl, Dictionary<string, object>? queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
        {
            return baseUrl;
        }

        var uriBuilder = new UriBuilder(baseUrl);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach (var (key, value) in queryParams)
        {
            query[key] = value?.ToString() ?? string.Empty;
        }

        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }

    private int GetEffectiveTimeout(int? requestedTimeout)
    {
        if (requestedTimeout == null)
        {
            return _options.DefaultTimeoutSeconds;
        }

        // Cap at max timeout
        return Math.Min(requestedTimeout.Value, _options.MaxTimeoutSeconds);
    }
}
