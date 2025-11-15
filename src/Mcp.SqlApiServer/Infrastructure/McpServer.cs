using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.SqlApiServer.Models;
using Mcp.SqlApiServer.Tools;
using Microsoft.Extensions.Logging;

namespace Mcp.SqlApiServer.Infrastructure;

/// <summary>
/// Core MCP server that handles JSON-RPC 2.0 protocol over stdio
/// </summary>
public sealed class McpServer
{
    private readonly HttpToolService _httpToolService;
    private readonly SqlToolService _sqlToolService;
    private readonly ILogger<McpServer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public McpServer(
        HttpToolService httpToolService,
        SqlToolService sqlToolService,
        ILogger<McpServer> logger)
    {
        _httpToolService = httpToolService;
        _sqlToolService = sqlToolService;
        _logger = logger;
    }

    /// <summary>
    /// Main loop: read requests from stdin, process, write responses to stdout
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MCP Server starting. Reading from stdin...");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);

                if (line == null)
                {
                    _logger.LogInformation("EOF received on stdin. Shutting down.");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var response = await ProcessRequestAsync(line, cancellationToken);
                    var responseJson = JsonSerializer.Serialize(response, JsonOptions);
                    await Console.Out.WriteLineAsync(responseJson);
                    await Console.Out.FlushAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request: {Message}", ex.Message);
                    // Don't crash the server on processing errors
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MCP Server shutting down due to cancellation.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in MCP server: {Message}", ex.Message);
            throw;
        }
    }

    private async Task<McpResponse> ProcessRequestAsync(string requestJson, CancellationToken cancellationToken)
    {
        McpRequest? request = null;

        try
        {
            // Parse request
            request = JsonSerializer.Deserialize<McpRequest>(requestJson, JsonOptions);

            if (request == null)
            {
                return CreateErrorResponse(null, McpErrorCodes.ParseError, "Failed to parse request");
            }

            _logger.LogDebug("Received request: method={Method}, id={Id}", request.Method, request.Id);

            // Dispatch to appropriate handler
            var result = request.Method switch
            {
                "initialize" => await HandleInitializeAsync(request, cancellationToken),
                "tools/list" => HandleToolsList(request),
                "tools/call" => await HandleToolsCallAsync(request, cancellationToken),
                _ => CreateErrorResponse(request.Id, McpErrorCodes.MethodNotFound, $"Method '{request.Method}' not found")
            };

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error: {Message}", ex.Message);
            return CreateErrorResponse(request?.Id, McpErrorCodes.ParseError, $"JSON parse error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing request: {Message}", ex.Message);
            return CreateErrorResponse(request?.Id, McpErrorCodes.InternalError, $"Internal error: {ex.Message}");
        }
    }

    private Task<McpResponse> HandleInitializeAsync(McpRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling initialize request");

        var result = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { }
            },
            serverInfo = new
            {
                name = "mcp-sqlapi-server",
                version = "1.0.0"
            }
        };

        return Task.FromResult(new McpResponse
        {
            Id = request.Id,
            Result = result
        });
    }

    private McpResponse HandleToolsList(McpRequest request)
    {
        _logger.LogInformation("Handling tools/list request");

        var tools = ToolDefinitions.GetAll();
        var result = new ToolsListResult { Tools = tools };

        return new McpResponse
        {
            Id = request.Id,
            Result = result
        };
    }

    private async Task<McpResponse> HandleToolsCallAsync(McpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Params == null)
            {
                return CreateErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params");
            }

            // Parse tool call params
            var paramsJson = JsonSerializer.Serialize(request.Params, JsonOptions);
            var toolCallParams = JsonSerializer.Deserialize<ToolCallParams>(paramsJson, JsonOptions);

            if (toolCallParams == null || string.IsNullOrWhiteSpace(toolCallParams.Name))
            {
                return CreateErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing tool name");
            }

            _logger.LogInformation("Calling tool: {ToolName}", toolCallParams.Name);

            // Dispatch to appropriate tool
            var toolResult = toolCallParams.Name switch
            {
                "http.call" => await ExecuteHttpCallAsync(toolCallParams.Arguments, cancellationToken),
                "sql.query" => await ExecuteSqlQueryAsync(toolCallParams.Arguments, cancellationToken),
                "sql.execute" => await ExecuteSqlExecuteAsync(toolCallParams.Arguments, cancellationToken),
                _ => throw new InvalidOperationException($"Unknown tool: {toolCallParams.Name}")
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = toolResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: {Message}", ex.Message);
            return CreateErrorResponse(request.Id, McpErrorCodes.InternalError, ex.Message);
        }
    }

    private async Task<ToolResult> ExecuteHttpCallAsync(
        Dictionary<string, object>? arguments,
        CancellationToken cancellationToken)
    {
        var argsJson = JsonSerializer.Serialize(arguments, JsonOptions);
        var httpParams = JsonSerializer.Deserialize<HttpCallParams>(argsJson, JsonOptions);

        if (httpParams == null)
        {
            throw new ArgumentException("Invalid http.call arguments");
        }

        var result = await _httpToolService.CallAsync(httpParams, cancellationToken);
        var resultJson = JsonSerializer.Serialize(result, JsonOptions);

        return new ToolResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = resultJson }
            }
        };
    }

    private async Task<ToolResult> ExecuteSqlQueryAsync(
        Dictionary<string, object>? arguments,
        CancellationToken cancellationToken)
    {
        var argsJson = JsonSerializer.Serialize(arguments, JsonOptions);
        var sqlParams = JsonSerializer.Deserialize<SqlQueryParams>(argsJson, JsonOptions);

        if (sqlParams == null)
        {
            throw new ArgumentException("Invalid sql.query arguments");
        }

        var result = await _sqlToolService.QueryAsync(sqlParams, cancellationToken);
        var resultJson = JsonSerializer.Serialize(result, JsonOptions);

        return new ToolResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = resultJson }
            }
        };
    }

    private async Task<ToolResult> ExecuteSqlExecuteAsync(
        Dictionary<string, object>? arguments,
        CancellationToken cancellationToken)
    {
        var argsJson = JsonSerializer.Serialize(arguments, JsonOptions);
        var sqlParams = JsonSerializer.Deserialize<SqlExecuteParams>(argsJson, JsonOptions);

        if (sqlParams == null)
        {
            throw new ArgumentException("Invalid sql.execute arguments");
        }

        var result = await _sqlToolService.ExecuteAsync(sqlParams, cancellationToken);
        var resultJson = JsonSerializer.Serialize(result, JsonOptions);

        return new ToolResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = resultJson }
            }
        };
    }

    private static McpResponse CreateErrorResponse(object? id, int code, string message)
    {
        return new McpResponse
        {
            Id = id,
            Error = new McpError
            {
                Code = code,
                Message = message
            }
        };
    }
}
