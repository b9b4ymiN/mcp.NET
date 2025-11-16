# Testing with MCP Inspector

This guide explains how to test and debug the MCP SQL/API server using MCP Inspector and the built-in HTTP endpoints.

## Table of Contents
- [Quick Start](#quick-start)
- [Using MCP Inspector](#using-mcp-inspector)
- [HTTP Endpoints](#http-endpoints)
- [Testing Examples](#testing-examples)
- [Troubleshooting](#troubleshooting)

---

## Quick Start

### Start Server in HTTP Mode

```bash
# Method 1: Command line argument
dotnet run --project src/Mcp.SqlApiServer http

# Method 2: Environment variable
export MCP_TRANSPORT_MODE=http
dotnet run --project src/Mcp.SqlApiServer

# Method 3: Custom port
export MCP_HTTP_PORT=8080
dotnet run --project src/Mcp.SqlApiServer http
```

The server will start on `http://localhost:5000` (or your custom port) with the following endpoints:
- `GET  /tools` - List all available MCP tools
- `GET  /health` - Health check endpoint
- `POST /mcp` - MCP Inspector compatible endpoint (JSON-RPC 2.0)
- `POST /tools/{name}` - Execute specific tool via REST

---

## Using MCP Inspector

[MCP Inspector](https://github.com/modelcontextprotocol/inspector) is a visual debugging tool for MCP servers.

### Option 1: Connect via HTTP (Recommended)

1. **Start MCP server in HTTP mode:**
   ```bash
   dotnet run --project src/Mcp.SqlApiServer http
   ```

2. **Open MCP Inspector:**
   - Visit https://inspector.modelcontextprotocol.io
   - Or run locally: `npx @modelcontextprotocol/inspector`

3. **Connect to server:**
   - Select "HTTP Transport"
   - Enter URL: `http://localhost:5000/mcp`
   - Click "Connect"

4. **Test tools:**
   - Browse available tools in the left panel
   - Select a tool to see its schema
   - Fill in parameters and click "Execute"
   - View results in real-time

### Option 2: Connect via Stdio

1. **Configure MCP Inspector to use stdio:**
   ```bash
   npx @modelcontextprotocol/inspector dotnet run --project /path/to/mcp.NET/src/Mcp.SqlApiServer
   ```

2. The inspector will automatically connect via stdin/stdout

---

## HTTP Endpoints

### 1. GET /tools

**Description:** Returns a list of all available MCP tools with their schemas.

**Request:**
```bash
curl http://localhost:5000/tools
```

**Response:**
```json
{
  "tools": [
    {
      "name": "http.call",
      "description": "Make HTTP requests to external APIs...",
      "inputSchema": {
        "type": "object",
        "properties": {
          "method": { "type": "string", "default": "GET" },
          "url": { "type": "string" },
          ...
        },
        "required": ["url"]
      }
    },
    {
      "name": "sql.query",
      "description": "Execute SELECT queries on SQL Server...",
      "inputSchema": { ... }
    },
    {
      "name": "sql.execute",
      "description": "Execute INSERT, UPDATE, DELETE statements...",
      "inputSchema": { ... }
    }
  ],
  "count": 3
}
```

### 2. GET /health

**Description:** Health check endpoint for monitoring.

**Request:**
```bash
curl http://localhost:5000/health
```

**Response:**
```json
{
  "status": "healthy",
  "server": "mcp-sqlapi-server",
  "version": "1.0.0",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### 3. POST /mcp

**Description:** MCP Inspector compatible endpoint. Accepts JSON-RPC 2.0 requests.

**Request - List Tools:**
```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list"
  }'
```

**Request - Call Tool:**
```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
      "name": "http.call",
      "arguments": {
        "method": "GET",
        "url": "https://api.github.com/users/octocat"
      }
    }
  }'
```

### 4. POST /tools/{toolName}

**Description:** Simplified REST-style endpoint to execute tools directly.

**Request - HTTP Call:**
```bash
curl -X POST http://localhost:5000/tools/http.call \
  -H "Content-Type: application/json" \
  -d '{
    "method": "GET",
    "url": "https://api.github.com/users/octocat"
  }'
```

**Response:**
```json
{
  "statusCode": 200,
  "headers": {
    "content-type": "application/json; charset=utf-8",
    ...
  },
  "bodyText": "{\"login\":\"octocat\",\"id\":583231,...}"
}
```

**Request - SQL Query:**
```bash
curl -X POST http://localhost:5000/tools/sql.query \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "SELECT TOP 10 * FROM Users WHERE Status = @Status",
    "parameters": {
      "@Status": "active"
    }
  }'
```

**Response:**
```json
{
  "rows": [
    {"Id": 1, "Name": "John Doe", "Status": "active"},
    {"Id": 2, "Name": "Jane Smith", "Status": "active"}
  ],
  "rowCount": 2
}
```

**Request - SQL Execute:**
```bash
curl -X POST http://localhost:5000/tools/sql.execute \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "UPDATE Users SET LastLogin = GETDATE() WHERE Id = @Id",
    "parameters": {
      "@Id": 123
    }
  }'
```

**Response:**
```json
{
  "rowsAffected": 1
}
```

---

## Testing Examples

### Example 1: Test Tool Discovery

```bash
# List all tools
curl http://localhost:5000/tools | jq '.tools[].name'

# Expected output:
# "http.call"
# "sql.query"
# "sql.execute"
```

### Example 2: Test HTTP Tool

```bash
# Fetch GitHub user data
curl -X POST http://localhost:5000/tools/http.call \
  -H "Content-Type: application/json" \
  -d '{
    "method": "GET",
    "url": "https://api.github.com/users/octocat"
  }' | jq '.bodyText | fromjson | {login, name, public_repos}'
```

### Example 3: Test SQL Query Tool

```bash
# Query database
curl -X POST http://localhost:5000/tools/sql.query \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "SELECT COUNT(*) AS TotalUsers FROM Users"
  }' | jq '.rows'
```

### Example 4: Full JSON-RPC Flow

```bash
# Initialize connection
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {}
  }' | jq

# List tools
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list"
  }' | jq '.result.tools[].name'

# Execute tool
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "sql.query",
      "arguments": {
        "sql": "SELECT @@VERSION AS SqlVersion"
      }
    }
  }' | jq '.result.content[0].text | fromjson'
```

---

## Troubleshooting

### Server Won't Start in HTTP Mode

**Symptom:** Error when starting with `http` argument

**Solution:**
```bash
# Check .NET SDK version (should be 8.0+)
dotnet --version

# Verify no port conflicts
lsof -i :5000  # macOS/Linux
netstat -ano | findstr :5000  # Windows

# Try custom port
export MCP_HTTP_PORT=8080
dotnet run --project src/Mcp.SqlApiServer http
```

### CORS Errors in MCP Inspector

**Symptom:** "Access-Control-Allow-Origin" error in browser console

**Solution:** CORS is already enabled for all origins in HTTP mode. If you still see errors:
- Ensure you're using the `/mcp` endpoint
- Try accessing from the same machine: `http://localhost:5000/mcp`
- Check browser console for actual error details

### Tools Returning Errors

**Symptom:** Tool execution fails with error messages

**Common Issues:**

1. **SQL Connection Error:**
   ```
   Error: SQL connection failed
   ```
   - Check `Sql:ConnectionString` in appsettings.json
   - Verify SQL Server is running
   - Test connection: `dotnet run --project src/Mcp.SqlApiServer` (stdio mode shows connection errors on startup)

2. **HTTP Host Blocked:**
   ```
   Error: Host 'example.com' is not in the allowed hosts list
   ```
   - Add host to `HttpTool:AllowedHosts` in appsettings.json
   - Or set `HttpTool:AllowAllHosts: true` (development only)

3. **DDL Operation Blocked:**
   ```
   Error: DDL operations are not allowed
   ```
   - This is a security feature
   - Set `Sql:BlockDdlOperations: false` if needed (not recommended)

### JSON-RPC Request Format Issues

**Symptom:** "Invalid JSON-RPC request" error

**Solution:** Ensure your request has the correct format:
```json
{
  "jsonrpc": "2.0",         // Required - must be "2.0"
  "id": 1,                  // Required - any number or string
  "method": "tools/call",   // Required - method name
  "params": { ... }         // Optional - depends on method
}
```

---

## Production vs Development

### Development Mode (HTTP)
- Enable CORS for all origins
- Verbose logging
- Detailed error messages
- Tool discovery endpoints

```bash
# Development
export MCP_TRANSPORT_MODE=http
export MCP_HTTP_PORT=5000
dotnet run --project src/Mcp.SqlApiServer
```

### Production Mode (Stdio)
- Stdio transport only (default)
- No HTTP exposure
- Logs to stderr
- Secure by design

```bash
# Production (default)
dotnet run --project src/Mcp.SqlApiServer
```

**Security Note:** Never expose the HTTP mode endpoints to the public internet without proper authentication and authorization. HTTP mode is designed for local testing and debugging only.

---

## Integration with CI/CD

### Automated Testing

```yaml
# GitHub Actions example
- name: Start MCP Server
  run: |
    dotnet run --project src/Mcp.SqlApiServer http &
    sleep 3

- name: Test Health Endpoint
  run: |
    curl -f http://localhost:5000/health

- name: Test Tool Discovery
  run: |
    curl -f http://localhost:5000/tools | jq '.count' | grep 3

- name: Test Tool Execution
  run: |
    curl -X POST http://localhost:5000/tools/http.call \
      -H "Content-Type: application/json" \
      -d '{"method":"GET","url":"https://httpbin.org/get"}' \
      | jq '.statusCode' | grep 200
```

---

## Additional Resources

- [MCP Protocol Specification](https://modelcontextprotocol.io/docs)
- [MCP Inspector GitHub](https://github.com/modelcontextprotocol/inspector)
- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)
