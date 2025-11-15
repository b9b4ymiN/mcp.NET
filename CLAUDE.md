# CLAUDE.md — C#/.NET MCP Server for API + SQL Server

You are Claude Code, an agentic coding assistant helping me build and maintain a **C#/.NET MCP server**.

This `CLAUDE.md` follows Anthropic's **Claude Code best practices**:
- Clear project goals and non‑goals
- Explicit tech stack and environment
- Concrete workflows for how you should help
- Well‑defined tools, commands, and constraints
- Preference for **spec-first, iterative, diff-based** changes

The repo you're in is dedicated to this MCP server only.

---

## 📊 Current Implementation Status

**Repository State**: Initial setup phase

**What exists**:
- ✅ CLAUDE.md specification document (this file)
- ✅ Git repository initialized
- ✅ Working branch: `claude/claude-md-mi0ggt2psvnsqtlh-01LER9FWDULK5Yex4FgKkstD`

**What needs to be implemented**:
- ⏳ .NET solution and project structure
- ⏳ MCP protocol implementation (stdio transport)
- ⏳ Core services (HttpToolService, SqlToolService)
- ⏳ Configuration system
- ⏳ Test suite
- ⏳ Documentation (README, API docs)

**Next Steps**: When ready to begin implementation, scaffold the .NET projects as described in Section 9.

---

## 1. Project Overview

### 🎯 Goal

Build a **Model Context Protocol (MCP) server** in **C#/.NET** that exposes tools for:

1. **Calling external HTTP APIs**
   - `http.call` : generic REST client

2. **CRUD operations on a Microsoft SQL Server database**
   - `sql.query`   : `SELECT` / read operations
   - `sql.execute` : `INSERT / UPDATE / DELETE` and other non‑query statements

The server will be used by:
- Claude Desktop / Claude Code via MCP
- My own Orchestrator and agents (in other projects) as a “Swiss-army knife” for APIs + SQL Server

Primary design goals:
- **Robust, strongly‑typed, production‑ready C# code**
- **Fast and efficient** (async I/O, proper connection handling)
- **Safe enough** to be run in real environments (with some guardrails)
- Easy for me (a .NET dev) to extend with new tools later

Non‑goals (for now):
- No fancy UI or dashboard
- No complex business logic — this layer should stay **generic + reusable**
- No streaming response protocol yet (simple request/response MCP is fine)

---

## 2. Tech Stack & Environment

### Language & runtime

- **C#** (latest stable version supported by .NET 8)
- **.NET 8** (ASP.NET Core)

### Packages / libraries (preferred)

- **Web host / HTTP**: ASP.NET Core Minimal APIs
- **JSON**: `System.Text.Json` (built‑in, fast, configurable)
- **SQL Server**:
  - Option A (recommended): **Dapper** for lightweight, fast data access
  - Option B: `Microsoft.Data.SqlClient` directly (if we want low-level control)

Use **async/await** throughout for I/O‑bound work (HTTP + DB).

### Configuration

- Use `appsettings.json` / environment variables for:
  - SQL Server connection string(s)
  - Allowed base URLs for outbound HTTP (for safety)
  - Optional logging configuration

---

## 3. High-Level Architecture

### 3.1 Components

- **McpServer** (core)
  - Handles MCP protocol (JSON‑RPC 2.0 style)
  - Registers available tools:
    - `http.call`
    - `sql.query`
    - `sql.execute`

- **HttpToolService**
  - Validates request arguments
  - Enforces safety rules (e.g., allowed domains, max timeout)
  - Executes outgoing HTTP requests (using `HttpClientFactory`)

- **SqlToolService**
  - Manages SQL Server connections via connection string(s)
  - Provides methods for:
    - `QueryAsync(string sql, object? parameters)` → returns rows
    - `ExecuteAsync(string sql, object? parameters)` → returns rows affected
  - Handles parameter binding
  - Applies simple protections where possible (prefers parameterized queries)

- **Config & Logging**
  - Typed `Options` classes for config
  - Logging via `ILogger<>` and `Microsoft.Extensions.Logging`
  - Minimal but helpful logs (no secrets in logs)

### 3.2 Project structure (target)

```text
src/
  Mcp.SqlApiServer/
    Program.cs
    Endpoints/
      McpEndpoints.cs          # entry MCP endpoint(s)
    Tools/
      HttpToolService.cs       # http.call implementation
      SqlToolService.cs        # sql.query / sql.execute implementation
    Models/
      McpRequest.cs            # MPC protocol structs
      McpResponse.cs
      ToolDefinitions.cs       # tool metadata (for MCP)
      HttpModels.cs            # http.call args/results
      SqlModels.cs             # sql.* args/results
    Config/
      HttpToolOptions.cs       # allowed hosts, timeout, etc
      SqlOptions.cs            # connection strings, default database
    Infrastructure/
      ErrorHandlingMiddleware.cs
      ValidationHelpers.cs
  Mcp.SqlApiServer.Tests/
    HttpToolTests.cs
    SqlToolTests.cs

appsettings.json
appsettings.Development.json
```

You can adjust structure to idiomatic .NET patterns as needed, but keep it **clean and discoverable**.

---

## 4. MCP Tools – API Surface

### 4.1 `http.call`

Generic HTTP client tool.

**Tool ID**
- `"http.call"`

**Input model (C#)** — rough shape:

```csharp
public sealed class HttpCallParams
{
    public string Method { get; set; } = "GET";       // e.g. GET, POST, PUT, DELETE
    public string Url { get; set; } = string.Empty;   // full URL
    public Dictionary<string, string>? Headers { get; set; }
    public object? Query { get; set; }                // key-value for query string
    public object? Body { get; set; }                 // optional JSON body
    public int? TimeoutSeconds { get; set; }          // optional override
}
```

**Output model**:

```csharp
public sealed class HttpCallResult
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string BodyText { get; set; } = string.Empty;
}
```

**Notes / constraints**:

- Enforce **allowed hostnames** (config-driven) so the tool can’t hit arbitrary internal networks by default.
- Implement reasonable **max timeout** and optional per‑call override with caps.
- For now, **no file uploads**; keep it JSON/text only.

---

### 4.2 `sql.query`

Read‑only queries (e.g., `SELECT`).

**Tool ID**
- `"sql.query"`

**Input model**:

```csharp
public sealed class SqlQueryParams
{
    public string Sql { get; set; } = string.Empty;       // text of SELECT query
    public Dictionary<string, object?>? Parameters { get; set; }  // @Name -> value
}
```

**Output**:

```csharp
public sealed class SqlQueryResult
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}
```

- Each row is a dict: column name → value
- Values may be string/number/bool/DateTime/null etc.

**Constraints / safety**:

- This tool is **intended for SELECT statements only**.
- If possible, add a basic guard:
  - reject queries containing obvious `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE` tokens (case‑insensitive).

---

### 4.3 `sql.execute`

Write operations: `INSERT`, `UPDATE`, `DELETE`, etc.

**Tool ID**
- `"sql.execute"`

**Input**:

```csharp
public sealed class SqlExecuteParams
{
    public string Sql { get; set; } = string.Empty;       // non-SELECT (INSERT/UPDATE/DELETE)
    public Dictionary<string, object?>? Parameters { get; set; }
}
```

**Output**:

```csharp
public sealed class SqlExecuteResult
{
    public int RowsAffected { get; set; }
}
```

**Constraints / safety**:

- Primarily for **DML** (INSERT/UPDATE/DELETE). Avoid DDL (DROP TABLE, etc.) unless explicitly allowed.
- Prefer parameterized queries over string concatenation.
- Consider optional config flags:
  - disallow `DROP`, `TRUNCATE`, destructive schema changes
  - restrict to specific schemas/tables in production

---

## 5. MCP Protocol Implementation

### 5.1 Protocol Overview

The Model Context Protocol (MCP) is a **JSON-RPC 2.0** based protocol that uses **stdio transport** for communication.

**Key protocol methods to implement**:
- `initialize` - Handshake to establish connection and capabilities
- `tools/list` - Return list of available tools with their schemas
- `tools/call` - Execute a specific tool with provided arguments
- `notifications/tools/list_changed` - (Optional) Notify clients when tools change

### 5.2 Transport Layer

**Stdio Transport** (recommended for MCP servers):
- Server reads JSON-RPC requests from **stdin**
- Server writes JSON-RPC responses to **stdout**
- Errors and logs go to **stderr**
- Each message is a single line of JSON (newline-delimited)

**Implementation approach**:
```csharp
// Pseudocode for Program.cs
while (await Console.In.ReadLineAsync() is string line)
{
    var request = JsonSerializer.Deserialize<McpRequest>(line);
    var response = await HandleRequest(request);
    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response));
}
```

### 5.3 Message Formats

**Request** (from client to server):
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "sql.query",
    "arguments": {
      "sql": "SELECT * FROM Users WHERE Id = @Id",
      "parameters": { "@Id": 123 }
    }
  }
}
```

**Response** (from server to client):
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"rows\": [{\"Id\": 123, \"Name\": \"John\"}]}"
      }
    ]
  }
}
```

**Error Response**:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32600,
    "message": "Invalid SQL syntax",
    "data": { "details": "..." }
  }
}
```

### 5.4 Tool Schema Definition

Each tool must provide a JSON Schema describing its parameters:

```csharp
public static class ToolDefinitions
{
    public static ToolMetadata[] GetAll() => new[]
    {
        new ToolMetadata
        {
            Name = "sql.query",
            Description = "Execute a SELECT query on SQL Server",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    sql = new { type = "string", description = "SQL SELECT statement" },
                    parameters = new
                    {
                        type = "object",
                        description = "Named parameters for the query",
                        additionalProperties = true
                    }
                },
                required = new[] { "sql" }
            }
        }
    };
}
```

### 5.5 Implementation Requirements

- **Strict JSON-RPC 2.0 compliance**: Include `jsonrpc: "2.0"` in all responses
- **ID matching**: Response `id` must match request `id`
- **Error codes**: Use standard JSON-RPC error codes:
  - `-32700`: Parse error
  - `-32600`: Invalid request
  - `-32601`: Method not found
  - `-32602`: Invalid params
  - `-32603`: Internal error
- **Async processing**: All tool methods should be async
- **Graceful shutdown**: Handle SIGTERM/SIGINT for clean shutdown

---

## 6. Development & Run Instructions

### 6.1 CLI

Preferred commands (you can adjust but keep similar):

```bash
# from repo root
dotnet restore
dotnet build

# run server (dev)
dotnet run --project src/Mcp.SqlApiServer

# run tests
dotnet test
```

Please:

- Add a **README** with these commands.
- Optionally add `.vscode/launch.json` or `.run` configs for easy debugging.

### 6.2 Configuration

Example `appsettings.Development.json`:

```jsonc
{
  "Sql": {
    "ConnectionString": "Server=localhost;Database=MyDb;User Id=sa;Password=Your_password123;Encrypt=False"
  },
  "HttpTool": {
    "AllowedHosts": [
      "api.github.com",
      "example.com"
    ],
    "DefaultTimeoutSeconds": 15,
    "MaxTimeoutSeconds": 60
  }
}
```

Make sure the server **fails fast** at startup if required config is missing.

---

## 7. How Claude Should Work Here (Best Practices)

Follow Anthropic’s Claude Code best practices:

### 7.1 General style

- Prefer **small, iterative changes**, not huge refactors in one shot.
- Before editing code, **summarize your plan** briefly.
- Use **spec-first** when building new features:
  1. Draft or update a markdown spec (or this file)
  2. Implement code following that spec
  3. Add tests where appropriate

### 7.2 Editing code

- When editing, prefer **diff‑style changes**:
  - Show which files to create or modify
  - Use clear headings per file
- Keep code **idiomatic C#**:
  - Use async/await, `using`/`await using` where appropriate
  - Use dependency injection (constructor injection) for services
  - Keep methods small and focused

### 7.3 Error handling & logging

- On errors:
  - Return **structured error responses** to the MCP client
  - Log stack traces server‑side
  - Do **not** leak secrets / connection strings into logs or error messages

### 7.4 Asking for clarification

In this repo, you may:

- Ask me for:
  - SQL schema / table definitions
  - Example queries or business rules
  - Details of target APIs to be called
- If something is unclear about how MCP client will use the server, **state your assumptions explicitly in comments**.

---

## 8. Safety & Constraints

Please keep these in mind:

- **Never hard‑code credentials** (SQL Server, API keys) in source code.
- Use environment variables or user‑secrets for production secrets.
- Avoid writing any code that can:
  - Drop or truncate tables by default
  - Connect back to unknown internal networks
- When generating example SQL:
  - Prefer parameterized syntax: `WHERE Id = @Id`, etc.
  - Avoid dynamic SQL string concatenation where possible.

If you need to generate “unsafe” examples (like DROP TABLE) for testing, keep them commented and clearly marked as dangerous.

---

## 9. Tasks I Want You To Start With

When I ask you to “set up MCP server project”, please:

1. Scaffold a new .NET solution and project:
   - `Mcp.SqlApiServer` as a web/console app hosting the MCP server
   - `Mcp.SqlApiServer.Tests` for tests
2. Implement:
   - Basic Program.cs with Minimal API / MCP endpoint skeleton
   - Models for MCP request/response and tool definitions
   - `HttpToolService` with `http.call`
   - `SqlToolService` with `sql.query` and `sql.execute`
3. Add a couple of small tests:
   - Unit tests for parameter validation
   - A “fake” SQL test using an in-memory or mocked connection (if reasonable)

Future tasks may include:
- Better schema validation for tool inputs
- Stronger safety checks (SQL, HTTP domains)
- Adding more tools (e.g., stored procedure executor, batched queries)

Use this `CLAUDE.md` as your **source of truth** for how the MCP server should behave.

---

## 10. Git Workflow & Version Control

### 10.1 Branch Strategy

- **Main branch**: `main` (or `master`) - production-ready code only
- **Feature branches**: Use descriptive names with prefix `feature/`, `fix/`, or `claude/`
  - Example: `claude/claude-md-mi0ggt2psvnsqtlh-01LER9FWDULK5Yex4FgKkstD` (current working branch)
- **Never push directly to main** - always use pull requests for review

### 10.2 Commit Messages

Follow conventional commit format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `refactor`: Code restructuring without behavior change
- `test`: Adding or updating tests
- `docs`: Documentation changes
- `chore`: Build process, dependencies, etc.

**Examples**:
```
feat(sql): implement sql.query tool with parameterized queries

- Add SqlToolService with QueryAsync method
- Implement parameter validation
- Add safety checks for non-SELECT statements

Closes #12
```

### 10.3 Pull Request Process

1. **Push feature branch** to remote
2. **Create PR** with clear description:
   - What changed and why
   - How to test
   - Any breaking changes
3. **Wait for review** (if working with team)
4. **Merge to main** after approval

---

## 11. Testing Strategy

### 11.1 Test Levels

**Unit Tests** (priority):
- Test individual services in isolation
- Mock external dependencies (HTTP clients, SQL connections)
- Fast execution (< 1 second per test)
- Coverage target: 80%+ for core logic

**Integration Tests**:
- Test against real SQL Server (use Docker container)
- Test actual HTTP calls to public APIs
- Slower execution (acceptable)
- Run in CI/CD pipeline

**End-to-End Tests** (optional):
- Full MCP protocol flow via stdio
- Simulate Claude Desktop client
- Run manually or in release pipeline

### 11.2 Test Organization

```
Mcp.SqlApiServer.Tests/
  Unit/
    Tools/
      HttpToolServiceTests.cs      # http.call unit tests
      SqlToolServiceTests.cs       # sql.query/execute unit tests
    Infrastructure/
      ValidationHelpersTests.cs
  Integration/
    SqlIntegrationTests.cs         # real SQL Server tests
    HttpIntegrationTests.cs        # real HTTP API tests
  E2E/
    McpProtocolTests.cs            # full protocol tests
```

### 11.3 Testing Best Practices

- **AAA pattern**: Arrange, Act, Assert
- **One assertion per test** (when feasible)
- **Descriptive test names**: `QueryAsync_WithValidParameters_ReturnsRows`
- **Use test fixtures** for shared setup (xUnit: `IClassFixture<>`)
- **Parameterized tests** for multiple scenarios (xUnit: `[Theory]`, `[InlineData]`)

Example:
```csharp
[Theory]
[InlineData("SELECT * FROM Users")]
[InlineData("select id, name from Products")]
public async Task QueryAsync_WithSelectStatement_Succeeds(string sql)
{
    // Arrange
    var service = new SqlToolService(mockConfig, mockLogger);

    // Act
    var result = await service.QueryAsync(sql, null);

    // Assert
    Assert.NotNull(result);
}
```

---

## 12. Development Environment Setup

### 12.1 Prerequisites

- **.NET 8 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/8.0))
- **SQL Server** (options):
  - Local SQL Server instance
  - Docker: `docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest`
  - Azure SQL Database
- **IDE** (recommended):
  - Visual Studio 2022
  - VS Code with C# Dev Kit
  - JetBrains Rider

### 12.2 First-Time Setup

```bash
# Clone repository
git clone <repo-url>
cd mcp.NET

# Restore dependencies
dotnet restore

# Setup user secrets (for connection strings)
cd src/Mcp.SqlApiServer
dotnet user-secrets init
dotnet user-secrets set "Sql:ConnectionString" "Server=localhost;Database=TestDb;User Id=sa;Password=YourPassword;Encrypt=False"

# Run tests
cd ../..
dotnet test

# Run server
dotnet run --project src/Mcp.SqlApiServer
```

### 12.3 VS Code Configuration

Create `.vscode/launch.json`:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Launch MCP Server",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/Mcp.SqlApiServer/bin/Debug/net8.0/Mcp.SqlApiServer.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/Mcp.SqlApiServer",
      "console": "integratedTerminal",
      "stopAtEntry": false
    }
  ]
}
```

### 12.4 Debugging Tips

- **Logging**: Use `ILogger<T>` heavily during development
- **Console debugging**: Since MCP uses stdio, redirect to file for debugging:
  ```bash
  dotnet run < input.txt > output.txt 2> errors.txt
  ```
- **Breakpoints**: Use conditional breakpoints for specific tool calls
- **Test explorer**: Use IDE test runners for quick feedback

---

## 13. Performance Considerations

### 13.1 SQL Server

- **Connection pooling**: Enabled by default in .NET
- **Async I/O**: Always use `async/await` for database calls
- **Parameterized queries**: Improves plan caching
- **Avoid N+1 queries**: Batch operations when possible
- **Indexes**: Document expected indexes in README

### 13.2 HTTP Calls

- **HttpClientFactory**: Reuse HttpClient instances
- **Timeouts**: Always set request timeouts
- **Retry logic** (future): Use Polly for transient failures
- **Connection limits**: Default is fine for most cases

### 13.3 Memory

- **Streaming**: For large result sets, consider streaming responses
- **Limits**: Add configurable limits (max rows, max response size)
- **Dispose pattern**: Properly dispose `SqlConnection`, `HttpResponseMessage`

---

## 14. Security Checklist

Before deploying to production:

- [ ] SQL injection prevention via parameterized queries
- [ ] HTTP allowlist enforced for `http.call`
- [ ] No credentials in source code or logs
- [ ] Connection strings use encryption in transit
- [ ] DDL operations (DROP, TRUNCATE) restricted or disabled
- [ ] Input validation on all tool parameters
- [ ] Error messages don't leak sensitive information
- [ ] Rate limiting on expensive operations (future)
- [ ] Audit logging for destructive operations

---

## 15. Troubleshooting

### Common Issues

**Issue**: "Server not responding"
- Check: Is process running? Use `ps aux | grep Mcp.SqlApiServer`
- Check: Are you sending valid JSON-RPC to stdin?

**Issue**: "SQL connection failed"
- Check: Connection string in `appsettings.json` or user-secrets
- Check: SQL Server is running and accessible
- Check: Firewall rules allow connection

**Issue**: "HTTP call blocked"
- Check: Target host in `HttpTool:AllowedHosts` config
- Check: Network connectivity to target

**Issue**: "Tool not found"
- Check: Tool name exactly matches registered name (case-sensitive)
- Check: `tools/list` returns expected tools

---

## 16. Resources

### MCP Protocol
- [MCP Specification](https://modelcontextprotocol.io/docs)
- [MCP Servers GitHub](https://github.com/modelcontextprotocol/servers)

### .NET Documentation
- [.NET 8 Docs](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [Dapper Documentation](https://github.com/DapperLib/Dapper)
- [System.Text.Json Guide](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-overview)

### Testing
- [xUnit Documentation](https://xunit.net/)
- [Moq Quickstart](https://github.com/moq/moq4)

---

## 17. Future Enhancements

Potential features for future iterations:

- **Additional Tools**:
  - `sql.storedproc` - Execute stored procedures
  - `sql.batch` - Execute multiple queries in a transaction
  - `http.graphql` - GraphQL query support

- **Advanced Features**:
  - Connection string selection per request (multi-database support)
  - Query result caching
  - Webhook support for async operations
  - Resource templates (saved queries, API configs)

- **Operational**:
  - Health check endpoint
  - Metrics/telemetry export (Prometheus, Application Insights)
  - Docker containerization
  - Kubernetes deployment manifests

---

**Last Updated**: 2025-11-15
**Document Version**: 1.1
**Implementation Status**: Pre-development (specification phase)
