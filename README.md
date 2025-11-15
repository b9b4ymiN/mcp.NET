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
        "Sql__ConnectionString": "Server=localhost;Database=MyDb;..."
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
