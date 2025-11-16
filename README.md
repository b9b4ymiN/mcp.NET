# MCP SQL/API Server

A **Model Context Protocol (MCP)** server implementation in C#/.NET that provides tools for:
- Making HTTP API calls to external services
- Executing SQL queries and commands on Microsoft SQL Server

This server enables Claude Desktop, Claude Code, and other MCP clients to interact with APIs and databases through a simple, secure interface.

## Features

### 🌐 HTTP API Tool (`http.call`)
- Support for GET, POST, PUT, DELETE, PATCH, and other HTTP methods
- Custom headers and query parameters
- JSON request/response handling
- Configurable timeout and host allowlist for security
- Automatic error handling and retry logic

### 🗄️ SQL Server Tools
- **`sql.query`**: Execute SELECT queries with parameterized inputs
- **`sql.execute`**: Execute INSERT, UPDATE, DELETE statements
- Built-in safety features:
  - DDL operation blocking (DROP, TRUNCATE, ALTER, etc.)
  - Query timeout limits
  - Maximum row limits
  - Parameterized query support to prevent SQL injection

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Microsoft SQL Server (local, Docker, or Azure SQL)
- (Optional) Visual Studio 2022, VS Code, or JetBrains Rider

## Quick Start

### 1. Clone and Build

```bash
git clone <repository-url>
cd mcp.NET

dotnet restore
dotnet build
```

### 2. Configure SQL Connection

The SQL Server connection string can be configured in multiple ways:

**Option A: User Secrets (recommended for development)**
```bash
cd src/Mcp.SqlApiServer
dotnet user-secrets init
dotnet user-secrets set "Sql:ConnectionString" "Server=localhost;Database=MyDb;User Id=sa;Password=YourPassword;Encrypt=False"
```

**Option B: Environment Variable**
```bash
export Sql__ConnectionString="Server=localhost;Database=MyDb;User Id=sa;Password=YourPassword;Encrypt=False"
```

**Option C: appsettings.Development.json**
```json
{
  "Sql": {
    "ConnectionString": "Server=localhost;Database=MyDb;User Id=sa;Password=YourPassword;Encrypt=False"
  }
}
```

### 3. Configure HTTP Allowed Hosts

Edit `appsettings.json` or `appsettings.Development.json`:

```json
{
  "HttpTool": {
    "AllowedHosts": [
      "api.github.com",
      "jsonplaceholder.typicode.com",
      "your-api-domain.com"
    ],
    "DefaultTimeoutSeconds": 30,
    "MaxTimeoutSeconds": 120
  }
}
```

### 4. Run the Server

```bash
dotnet run --project src/Mcp.SqlApiServer
```

The server will start and listen for MCP requests on stdin/stdout.

## Integration Options

This MCP server can be used in two main ways:

### Option 1: Claude Desktop Integration

Perfect for direct interaction with Claude Desktop application.

### Option 2: Mini-AGI Backend Integration

Integrate with the [mini-AGI_Backend](https://github.com/b9b4ymiN/mini-AGI_Backend) orchestration system to give your AI agents database and API capabilities.

---

## Usage with Claude Desktop

### 1. Configure Claude Desktop

Add to your Claude Desktop configuration file:

**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "sql-api": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/mcp.NET/src/Mcp.SqlApiServer"],
      "env": {
        "Sql__ConnectionString": "Server=localhost;Database=MyDb;User Id=sa;Password=YourPassword;Encrypt=False"
      }
    }
  }
}
```

### 2. Use Tools in Claude

Once configured, you can ask Claude to:

**Query a database:**
```
Can you query the Users table and show me the first 10 records?
```

**Make an HTTP API call:**
```
Can you fetch the latest repos for the user 'octocat' from GitHub API?
```

**Execute database updates:**
```
Can you update the status to 'active' for user ID 123?
```

---

## Usage with mini-AGI Backend

Integrate this MCP server with your [mini-AGI_Backend](https://github.com/b9b4ymiN/mini-AGI_Backend) orchestration system to enable AI agents to interact with databases and APIs.

### Architecture Overview

```
mini-AGI Backend (FastAPI)
    ↓
Agent Orchestrator (Orchestrator, Coder, Researcher)
    ↓
Tool Registry
    ├─→ Local Tools (file I/O, Python exec)
    ├─→ MCP Bridge Adapter
    │       ├─→ Filesystem MCP Server
    │       ├─→ Trader MCP Server
    │       └─→ SQL/API MCP Server (this project) ⭐
    └─→ External LLM Providers (Ollama, Z.AI)
```

### Prerequisites

1. **mini-AGI_Backend** repository cloned and set up
2. **This MCP server** built and configured
3. **.NET 8 SDK** installed
4. **SQL Server** accessible (if using SQL tools)

### Integration Steps

#### Step 1: Build MCP Server for Production

```bash
# In this repository (mcp.NET)
cd mcp.NET
dotnet publish src/Mcp.SqlApiServer -c Release -o ./publish
```

This creates a standalone executable in `./publish/` directory.

#### Step 2: Create MCP Bridge Adapter for mini-AGI

In your **mini-AGI_Backend** repository, create a new MCP bridge adapter:

**File**: `backend/tools/mcp_sqlapi_bridge.py`

```python
import os
import subprocess
import json
import logging
from typing import Dict, Any, Optional

logger = logging.getLogger(__name__)

class SqlApiMcpBridge:
    """Bridge adapter for the .NET MCP SQL/API Server"""

    def __init__(self, mcp_executable_path: str, connection_string: str, allowed_hosts: list = None):
        """
        Initialize the SQL/API MCP bridge

        Args:
            mcp_executable_path: Path to Mcp.SqlApiServer executable or DLL
            connection_string: SQL Server connection string
            allowed_hosts: List of allowed HTTP hosts
        """
        self.mcp_path = mcp_executable_path
        self.connection_string = connection_string
        self.allowed_hosts = allowed_hosts or ["api.github.com", "httpbin.org"]
        self.process: Optional[subprocess.Popen] = None

    def start(self):
        """Start the MCP server process"""
        env = {
            "Sql__ConnectionString": self.connection_string,
            "HttpTool__AllowedHosts__0": self.allowed_hosts[0] if self.allowed_hosts else "",
        }

        # Add all allowed hosts to environment
        for i, host in enumerate(self.allowed_hosts):
            env[f"HttpTool__AllowedHosts__{i}"] = host

        # Start the .NET MCP server
        self.process = subprocess.Popen(
            ["dotnet", self.mcp_path],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            env={**os.environ, **env},
            text=True,
            bufsize=1
        )
        logger.info("SQL/API MCP server started")

    def call_tool(self, tool_name: str, arguments: Dict[str, Any]) -> Dict[str, Any]:
        """
        Call a tool on the MCP server

        Args:
            tool_name: Name of the tool (http.call, sql.query, sql.execute)
            arguments: Tool arguments

        Returns:
            Tool execution result
        """
        if not self.process:
            self.start()

        # Build MCP request
        request = {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "tools/call",
            "params": {
                "name": tool_name,
                "arguments": arguments
            }
        }

        try:
            # Send request
            request_json = json.dumps(request) + "\n"
            self.process.stdin.write(request_json)
            self.process.stdin.flush()

            # Read response
            response_line = self.process.stdout.readline()
            response = json.loads(response_line)

            # Handle errors
            if "error" in response:
                error_msg = response["error"].get("message", "Unknown error")
                logger.error(f"MCP tool error: {error_msg}")
                return {"error": error_msg}

            # Extract result
            if "result" in response and "content" in response["result"]:
                content = response["result"]["content"]
                if content and len(content) > 0:
                    # Parse the JSON text from content
                    result_text = content[0].get("text", "{}")
                    return json.loads(result_text)

            return response.get("result", {})

        except Exception as e:
            logger.error(f"MCP bridge error: {e}")
            return {"error": str(e)}

    def stop(self):
        """Stop the MCP server process"""
        if self.process:
            self.process.terminate()
            self.process.wait()
            logger.info("SQL/API MCP server stopped")

# Tool wrapper functions for mini-AGI registry
def create_sqlapi_tools(connection_string: str, mcp_path: str, allowed_hosts: list = None):
    """
    Create tool functions for mini-AGI tool registry

    Returns:
        Dictionary of tool functions
    """
    bridge = SqlApiMcpBridge(mcp_path, connection_string, allowed_hosts)
    bridge.start()

    def http_call(method: str, url: str, headers: dict = None, query: dict = None,
                  body: dict = None, timeout_seconds: int = 30) -> str:
        """
        Make HTTP API calls to external services

        Args:
            method: HTTP method (GET, POST, PUT, DELETE)
            url: Full URL to call
            headers: Optional HTTP headers
            query: Optional query parameters
            body: Optional request body (JSON)
            timeout_seconds: Request timeout

        Returns:
            JSON string with status, headers, and body
        """
        args = {
            "method": method.upper(),
            "url": url,
            "headers": headers or {},
            "query": query or {},
            "body": body,
            "timeoutSeconds": timeout_seconds
        }

        result = bridge.call_tool("http.call", args)
        if "error" in result:
            return f"Error: {result['error']}"
        return json.dumps(result, indent=2)

    def sql_query(sql: str, parameters: dict = None) -> str:
        """
        Execute SELECT queries on SQL Server

        Args:
            sql: SQL SELECT statement
            parameters: Named parameters (e.g., {"@Id": 123})

        Returns:
            JSON string with query results
        """
        args = {
            "sql": sql,
            "parameters": parameters or {}
        }

        result = bridge.call_tool("sql.query", args)
        if "error" in result:
            return f"Error: {result['error']}"
        return json.dumps(result, indent=2)

    def sql_execute(sql: str, parameters: dict = None) -> str:
        """
        Execute INSERT, UPDATE, DELETE statements on SQL Server

        Args:
            sql: SQL statement (INSERT/UPDATE/DELETE)
            parameters: Named parameters

        Returns:
            JSON string with rows affected count
        """
        args = {
            "sql": sql,
            "parameters": parameters or {}
        }

        result = bridge.call_tool("sql.execute", args)
        if "error" in result:
            return f"Error: {result['error']}"
        return json.dumps(result, indent=2)

    return {
        "http_call": http_call,
        "sql_query": sql_query,
        "sql_execute": sql_execute
    }
```

#### Step 3: Register Tools in mini-AGI Backend

Update your `backend/tools/__init__.py` or tool registry file:

```python
import os
from backend.tools.mcp_sqlapi_bridge import create_sqlapi_tools

# Get configuration from environment
SQL_CONNECTION = os.getenv("SQL_CONNECTION_STRING", "")
MCP_SQLAPI_PATH = os.getenv("MCP_SQLAPI_PATH", "/path/to/mcp.NET/publish/Mcp.SqlApiServer.dll")
ALLOWED_API_HOSTS = os.getenv("ALLOWED_API_HOSTS", "api.github.com,httpbin.org").split(",")

# Create SQL/API tools
if SQL_CONNECTION:
    sqlapi_tools = create_sqlapi_tools(
        connection_string=SQL_CONNECTION,
        mcp_path=MCP_SQLAPI_PATH,
        allowed_hosts=ALLOWED_API_HOSTS
    )

    # Register with your tool registry
    TOOL_REGISTRY.update(sqlapi_tools)
```

#### Step 4: Configure Environment Variables

Add to your mini-AGI `.env` file:

```bash
# SQL/API MCP Server Configuration
SQL_CONNECTION_STRING=Server=localhost;Database=MyDb;User Id=sa;Password=YourPassword;Encrypt=False
MCP_SQLAPI_PATH=/absolute/path/to/mcp.NET/publish/Mcp.SqlApiServer.dll
ALLOWED_API_HOSTS=api.github.com,jsonplaceholder.typicode.com,httpbin.org
```

#### Step 5: Start mini-AGI Backend

```bash
cd mini-AGI_Backend
uvicorn backend.main:app --reload --port 8000
```

### Usage Examples with mini-AGI

Once integrated, your AI agents can use these tools:

**Example 1: Agent queries database**
```bash
curl -X POST http://localhost:8000/chat \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {
        "role": "user",
        "content": "Query the Users table and show me all active users"
      }
    ]
  }'
```

The orchestrator agent will:
1. Decide to use the `sql_query` tool
2. Execute: `sql_query("SELECT * FROM Users WHERE Status = @Status", {"@Status": "active"})`
3. Return formatted results

**Example 2: Agent fetches data from external API**
```bash
curl -X POST http://localhost:8000/chat \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {
        "role": "user",
        "content": "Fetch the latest 5 repositories from GitHub user octocat and save to database"
      }
    ]
  }'
```

The agent will:
1. Use `http_call("GET", "https://api.github.com/users/octocat/repos", query={"per_page": 5})`
2. Parse the API response
3. Use `sql_execute` to insert records into your database

**Example 3: Multi-step orchestration**
```bash
curl -X POST http://localhost:8000/chat \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {
        "role": "user",
        "content": "Check if user ID 123 exists, if not, create them by calling the registration API"
      }
    ]
  }'
```

### Benefits of Integration

✅ **Database Access**: AI agents can query and update SQL Server databases
✅ **API Integration**: Agents can fetch data from external APIs
✅ **Multi-Agent Coordination**: Orchestrator routes database/API tasks automatically
✅ **Type Safety**: .NET backend provides strong typing and validation
✅ **Security**: Built-in SQL injection prevention and HTTP host allowlisting
✅ **Traceability**: Full event traces show tool execution in agent reasoning

### Troubleshooting Integration

**Issue**: MCP server not starting
```
Check STDERR logs from the bridge adapter
Verify .NET 8 SDK is installed: dotnet --version
Ensure MCP_SQLAPI_PATH points to correct DLL or executable
```

**Issue**: SQL connection fails
```
Verify SQL_CONNECTION_STRING is correct
Test connection: dotnet run --project src/Mcp.SqlApiServer
Check SQL Server is accessible from mini-AGI backend host
```

**Issue**: HTTP calls blocked
```
Add target host to ALLOWED_API_HOSTS environment variable
Check HttpTool:AllowedHosts in MCP server configuration
```

## Configuration Reference

### SQL Options

| Setting | Description | Default |
|---------|-------------|---------|
| `ConnectionString` | SQL Server connection string | (required) |
| `BlockDdlOperations` | Block DROP, TRUNCATE, ALTER, etc. | `true` |
| `MaxRowsReturned` | Max rows returned from queries (0 = unlimited) | `10000` |
| `QueryTimeoutSeconds` | Query timeout in seconds | `30` |

### HTTP Tool Options

| Setting | Description | Default |
|---------|-------------|---------|
| `AllowedHosts` | List of allowed hostnames | `[]` |
| `DefaultTimeoutSeconds` | Default HTTP request timeout | `30` |
| `MaxTimeoutSeconds` | Maximum allowed timeout | `120` |
| `AllowAllHosts` | Allow requests to any host (⚠️ dev only) | `false` |

## Tool Schemas

### http.call

```json
{
  "name": "http.call",
  "arguments": {
    "method": "GET",
    "url": "https://api.github.com/users/octocat",
    "headers": {
      "Authorization": "Bearer token123"
    },
    "query": {
      "per_page": 10
    },
    "body": {
      "key": "value"
    },
    "timeoutSeconds": 60
  }
}
```

### sql.query

```json
{
  "name": "sql.query",
  "arguments": {
    "sql": "SELECT * FROM Users WHERE Id = @Id",
    "parameters": {
      "@Id": 123
    }
  }
}
```

### sql.execute

```json
{
  "name": "sql.execute",
  "arguments": {
    "sql": "UPDATE Users SET Status = @Status WHERE Id = @Id",
    "parameters": {
      "@Status": "active",
      "@Id": 123
    }
  }
}
```

## Development

### Run Tests

```bash
dotnet test
```

### Build for Release

```bash
dotnet build -c Release
dotnet publish -c Release -o ./publish
```

### Docker (Optional)

Run SQL Server in Docker:

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

## Security Considerations

- **Never commit connection strings** to source control
- Use **user-secrets** or environment variables for credentials
- Keep `BlockDdlOperations` enabled in production
- Configure `AllowedHosts` to prevent SSRF attacks
- Use **parameterized queries** to prevent SQL injection
- Review logs regularly (logged to stderr)

## Troubleshooting

### Connection String Not Found
```
ERROR: SQL connection string is not configured.
```
**Solution**: Set `Sql:ConnectionString` in user-secrets, environment variable, or appsettings.json

### Host Not Allowed
```
Host 'example.com' is not in the allowed hosts list
```
**Solution**: Add the host to `HttpTool:AllowedHosts` in configuration

### DDL Operation Blocked
```
DDL operations (DROP, TRUNCATE, ALTER, CREATE, etc.) are not allowed.
```
**Solution**: This is a safety feature. Set `BlockDdlOperations: false` if needed (not recommended for production)

## Architecture

```
MCP Client (Claude Desktop)
    ↓ (stdin/stdout - JSON-RPC 2.0)
McpServer (Protocol Handler)
    ↓
    ├─→ HttpToolService → External APIs
    └─→ SqlToolService → SQL Server
```

## Contributing

See [CLAUDE.md](./CLAUDE.md) for detailed development guidelines and architecture documentation.

## License

[Your License Here]

## Resources

- [MCP Protocol Specification](https://modelcontextprotocol.io/docs)
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [Dapper Documentation](https://github.com/DapperLib/Dapper)
