# CLAUDE.md — C#/.NET MCP Server for API + SQL Server

You are Claude Code, an agentic coding assistant helping me build and maintain a **C#/.NET MCP server**.

This `CLAUDE.md` follows Anthropic’s **Claude Code best practices**:
- Clear project goals and non‑goals
- Explicit tech stack and environment
- Concrete workflows for how you should help
- Well‑defined tools, commands, and constraints
- Preference for **spec-first, iterative, diff-based** changes

The repo you’re in is dedicated to this MCP server only.

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

## 5. MCP Protocol Shape

You don’t need to re‑implement MCP spec from scratch here; I will handle config in `.mcp.json` and Claude. But code should:

- Implement a small JSON‑RPC style handler:
  - Parse **tool invocation requests** (tool name + arguments)
  - Dispatch to the mapped C# tool methods
  - Return a JSON result, or structured error

- Define **tool metadata** (name, description, parameters schema) so clients can introspect the server’s capabilities.

Assume we will support:
- `tools/list` (or equivalent)  
- `tools/call`

Use simple, well‑typed models for request/response. I’d rather have correctness + clarity than super generic magic.

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
