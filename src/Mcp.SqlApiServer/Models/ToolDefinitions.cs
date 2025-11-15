using System.Text.Json.Serialization;

namespace Mcp.SqlApiServer.Models;

/// <summary>
/// Metadata for an MCP tool
/// </summary>
public sealed class ToolMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public object InputSchema { get; set; } = new();
}

/// <summary>
/// Response for tools/list method
/// </summary>
public sealed class ToolsListResult
{
    [JsonPropertyName("tools")]
    public List<ToolMetadata> Tools { get; set; } = new();
}

/// <summary>
/// Defines all available MCP tools
/// </summary>
public static class ToolDefinitions
{
    public static List<ToolMetadata> GetAll() => new()
    {
        new ToolMetadata
        {
            Name = "http.call",
            Description = "Make HTTP requests to external APIs. Supports GET, POST, PUT, DELETE methods with custom headers and body.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    method = new
                    {
                        type = "string",
                        description = "HTTP method (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS)",
                        @default = "GET"
                    },
                    url = new
                    {
                        type = "string",
                        description = "Full URL to call (must be in allowed hosts list)"
                    },
                    headers = new
                    {
                        type = "object",
                        description = "Optional HTTP headers as key-value pairs",
                        additionalProperties = new { type = "string" }
                    },
                    query = new
                    {
                        type = "object",
                        description = "Optional query string parameters as key-value pairs",
                        additionalProperties = true
                    },
                    body = new
                    {
                        type = "object",
                        description = "Optional request body (will be JSON serialized)"
                    },
                    timeoutSeconds = new
                    {
                        type = "integer",
                        description = "Optional timeout in seconds (max: configured limit)"
                    }
                },
                required = new[] { "url" }
            }
        },
        new ToolMetadata
        {
            Name = "sql.query",
            Description = "Execute SELECT queries on SQL Server. Returns rows as JSON. Read-only operation.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    sql = new
                    {
                        type = "string",
                        description = "SQL SELECT statement (parameterized queries recommended)"
                    },
                    parameters = new
                    {
                        type = "object",
                        description = "Named parameters for the query (e.g., { \"@Id\": 123 })",
                        additionalProperties = true
                    }
                },
                required = new[] { "sql" }
            }
        },
        new ToolMetadata
        {
            Name = "sql.execute",
            Description = "Execute INSERT, UPDATE, DELETE statements on SQL Server. Returns number of rows affected.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    sql = new
                    {
                        type = "string",
                        description = "SQL statement (INSERT, UPDATE, DELETE - use parameterized queries)"
                    },
                    parameters = new
                    {
                        type = "object",
                        description = "Named parameters for the statement (e.g., { \"@Name\": \"John\" })",
                        additionalProperties = true
                    }
                },
                required = new[] { "sql" }
            }
        }
    };
}
