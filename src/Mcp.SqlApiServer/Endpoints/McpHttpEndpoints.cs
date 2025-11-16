using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.SqlApiServer.Models;
using Mcp.SqlApiServer.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mcp.SqlApiServer.Endpoints;

/// <summary>
/// HTTP endpoints for MCP server (monitoring, testing, tool discovery)
/// </summary>
public static class McpHttpEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static void MapMcpEndpoints(this WebApplication app)
    {
        // Health check endpoint
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            server = "mcp-sqlapi-server",
            version = "1.0.0",
            timestamp = DateTime.UtcNow
        }));

        // Tools list endpoint - returns all available MCP tools
        app.MapGet("/tools", (ILogger<Program> logger) =>
        {
            logger.LogInformation("HTTP /tools endpoint called");

            var tools = ToolDefinitions.GetAll();
            var response = new
            {
                tools = tools.Select(t => new
                {
                    t.Name,
                    t.Description,
                    inputSchema = t.InputSchema
                }).ToList(),
                count = tools.Count
            };

            return Results.Ok(response);
        });

        // MCP Inspector compatible endpoint - responds to JSON-RPC requests via HTTP POST
        app.MapPost("/mcp", async (
            HttpContext context,
            HttpToolService httpToolService,
            SqlToolService sqlToolService,
            ILogger<Program> logger) =>
        {
            try
            {
                var requestBody = await context.Request.ReadFromJsonAsync<McpRequest>(JsonOptions);

                if (requestBody == null)
                {
                    return Results.BadRequest(new McpResponse
                    {
                        Id = null,
                        Error = new McpError
                        {
                            Code = McpErrorCodes.ParseError,
                            Message = "Invalid JSON-RPC request"
                        }
                    });
                }

                logger.LogInformation("HTTP MCP request: method={Method}, id={Id}",
                    requestBody.Method, requestBody.Id);

                var response = requestBody.Method switch
                {
                    "initialize" => HandleInitialize(requestBody),
                    "tools/list" => HandleToolsList(requestBody),
                    "tools/call" => await HandleToolsCallAsync(
                        requestBody, httpToolService, sqlToolService, logger),
                    _ => CreateErrorResponse(requestBody.Id, McpErrorCodes.MethodNotFound,
                        $"Method '{requestBody.Method}' not found")
                };

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing HTTP MCP request");
                return Results.Json(new McpResponse
                {
                    Error = new McpError
                    {
                        Code = McpErrorCodes.InternalError,
                        Message = ex.Message
                    }
                }, JsonOptions, statusCode: 500);
            }
        });

        // Tool execution endpoint - simplified REST-style access to tools
        app.MapPost("/tools/{toolName}", async (
            string toolName,
            HttpContext context,
            HttpToolService httpToolService,
            SqlToolService sqlToolService,
            ILogger<Program> logger) =>
        {
            try
            {
                var arguments = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>();

                logger.LogInformation("Executing tool {ToolName} via HTTP", toolName);

                var result = toolName switch
                {
                    "http.call" => await ExecuteHttpCallAsync(arguments, httpToolService),
                    "sql.query" => await ExecuteSqlQueryAsync(arguments, sqlToolService),
                    "sql.execute" => await ExecuteSqlExecuteAsync(arguments, sqlToolService),
                    _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
                };

                // Extract text from ToolResult
                if (result.Content.Count > 0)
                {
                    var resultText = result.Content[0].Text;
                    var resultObject = JsonSerializer.Deserialize<object>(resultText);
                    return Results.Ok(resultObject);
                }

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing tool {ToolName}", toolName);
                return Results.Json(new { error = ex.Message }, statusCode: 400);
            }
        });
    }

    private static McpResponse HandleInitialize(McpRequest request)
    {
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

        return new McpResponse
        {
            Id = request.Id,
            Result = result
        };
    }

    private static McpResponse HandleToolsList(McpRequest request)
    {
        var tools = ToolDefinitions.GetAll();
        var result = new ToolsListResult { Tools = tools };

        return new McpResponse
        {
            Id = request.Id,
            Result = result
        };
    }

    private static async Task<McpResponse> HandleToolsCallAsync(
        McpRequest request,
        HttpToolService httpToolService,
        SqlToolService sqlToolService,
        ILogger logger)
    {
        try
        {
            if (request.Params == null)
            {
                return CreateErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params");
            }

            var paramsJson = JsonSerializer.Serialize(request.Params, JsonOptions);
            var toolCallParams = JsonSerializer.Deserialize<ToolCallParams>(paramsJson, JsonOptions);

            if (toolCallParams == null || string.IsNullOrWhiteSpace(toolCallParams.Name))
            {
                return CreateErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing tool name");
            }

            var toolResult = toolCallParams.Name switch
            {
                "http.call" => await ExecuteHttpCallAsync(toolCallParams.Arguments, httpToolService),
                "sql.query" => await ExecuteSqlQueryAsync(toolCallParams.Arguments, sqlToolService),
                "sql.execute" => await ExecuteSqlExecuteAsync(toolCallParams.Arguments, sqlToolService),
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
            logger.LogError(ex, "Tool execution failed: {Message}", ex.Message);
            return CreateErrorResponse(request.Id, McpErrorCodes.InternalError, ex.Message);
        }
    }

    private static async Task<ToolResult> ExecuteHttpCallAsync(
        Dictionary<string, object>? arguments,
        HttpToolService httpToolService)
    {
        var argsJson = JsonSerializer.Serialize(arguments, JsonOptions);
        var httpParams = JsonSerializer.Deserialize<HttpCallParams>(argsJson, JsonOptions);

        if (httpParams == null)
        {
            throw new ArgumentException("Invalid http.call arguments");
        }

        var result = await httpToolService.CallAsync(httpParams);
        var resultJson = JsonSerializer.Serialize(result, JsonOptions);

        return new ToolResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = resultJson }
            }
        };
    }

    private static async Task<ToolResult> ExecuteSqlQueryAsync(
        Dictionary<string, object>? arguments,
        SqlToolService sqlToolService)
    {
        var argsJson = JsonSerializer.Serialize(arguments, JsonOptions);
        var sqlParams = JsonSerializer.Deserialize<SqlQueryParams>(argsJson, JsonOptions);

        if (sqlParams == null)
        {
            throw new ArgumentException("Invalid sql.query arguments");
        }

        var result = await sqlToolService.QueryAsync(sqlParams);
        var resultJson = JsonSerializer.Serialize(result, JsonOptions);

        return new ToolResult
        {
            Content = new List<ContentItem>
            {
                new ContentItem { Type = "text", Text = resultJson }
            }
        };
    }

    private static async Task<ToolResult> ExecuteSqlExecuteAsync(
        Dictionary<string, object>? arguments,
        SqlToolService sqlToolService)
    {
        var argsJson = JsonSerializer.Serialize(arguments, JsonOptions);
        var sqlParams = JsonSerializer.Deserialize<SqlExecuteParams>(argsJson, JsonOptions);

        if (sqlParams == null)
        {
            throw new ArgumentException("Invalid sql.execute arguments");
        }

        var result = await sqlToolService.ExecuteAsync(sqlParams);
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
