# MCP SQL/API Server

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![MCP Compatible](https://img.shields.io/badge/MCP-Compatible-purple.svg)](https://modelcontextprotocol.io)

A production-ready **Model Context Protocol (MCP)** server implementation in C#/.NET 8 that provides robust tooling for HTTP API integration and SQL Server database operations. Designed for enterprise applications requiring secure, type-safe data access through AI agents and automation systems.

## 📋 Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
  - [Step 1: Clone and Build](#step-1-clone-and-build)
  - [Step 2: Configure SQL Server](#step-2-configure-sql-server)
  - [Step 3: Configure HTTP Security](#step-3-configure-http-security)
  - [Step 4: Run the Server](#step-4-run-the-server)
- [Testing Guide](#testing-guide)
  - [Testing with HTTP Mode](#testing-with-http-mode)
  - [Testing with MCP Inspector](#testing-with-mcp-inspector)
  - [Testing Individual Tools](#testing-individual-tools)
- [Integration](#integration)
  - [Claude Desktop](#claude-desktop-integration)
  - [Mini-AGI Backend](#mini-agi-backend-integration)
- [Configuration](#configuration)
- [API Reference](#api-reference)
- [Security](#security)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

## Overview

The MCP SQL/API Server bridges the gap between AI systems and enterprise data sources, enabling secure programmatic access to:

- **HTTP/REST APIs**: Make authenticated requests to external services with configurable security policies
- **SQL Server Databases**: Execute parameterized queries and commands with built-in injection prevention

**Architecture:**
```
┌─────────────────────┐
│   MCP Client        │
│ (Claude, mini-AGI)  │
└──────────┬──────────┘
           │ JSON-RPC 2.0
           │ (stdio/HTTP)
┌──────────▼──────────┐
│  MCP Server         │
│  (This Project)     │
├─────────────────────┤
│ • HTTP Tool Service │──► External APIs
│ • SQL Tool Service  │──► SQL Server
└─────────────────────┘
```

## Features

### 🌐 HTTP API Tool (`http.call`)

Execute HTTP requests with enterprise-grade security:

- **Methods**: GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS
- **Authentication**: Custom headers support (Bearer tokens, API keys)
- **Query Parameters**: Dynamic parameter injection
- **Request Body**: JSON serialization
- **Security**: Host allowlisting, configurable timeouts
- **Error Handling**: Structured error responses with retry logic

### 🗄️ SQL Server Tools

Secure database operations with multiple safety layers:

- **`sql.query`**: Execute SELECT queries with row limiting
- **`sql.execute`**: Execute INSERT, UPDATE, DELETE with audit trails

**Security Features:**
- ✅ Parameterized query enforcement
- ✅ DDL operation blocking (DROP, TRUNCATE, ALTER)
- ✅ Query timeout limits
- ✅ Maximum row return limits
- ✅ SQL injection prevention
- ✅ Transaction support

### 🔧 Dual Transport Mode

- **Stdio Transport** (default): Production-ready MCP protocol
- **HTTP Transport**: Testing and monitoring with REST endpoints

## Prerequisites

Before you begin, ensure you have the following installed:

| Requirement | Version | Purpose | Download |
|-------------|---------|---------|----------|
| .NET SDK | 8.0+ | Runtime and build tools | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| SQL Server | 2019+ | Database (optional for HTTP-only) | [Download](https://www.microsoft.com/sql-server) |
| Git | Latest | Source control | [Download](https://git-scm.com/) |

**Optional Tools:**
- Visual Studio 2022 / VS Code / JetBrains Rider
- Docker Desktop (for SQL Server container)
- curl or Postman (for HTTP testing)

**Verify Prerequisites:**
```bash
# Check .NET SDK
dotnet --version
# Expected output: 8.0.x or higher

# Check Git
git --version
# Expected output: git version 2.x.x
```

## Getting Started

Follow these steps to get the MCP server running on your local machine.

### Step 1: Clone and Build

**1.1 Clone the Repository**
```bash
git clone https://github.com/b9b4ymiN/mcp.NET.git
cd mcp.NET
```

**1.2 Verify Repository Structure**
```bash
ls -la
# You should see: src/, CLAUDE.md, README.md, etc.
```

**1.3 Restore Dependencies**
```bash
dotnet restore
```

**Expected Output:**
```
Determining projects to restore...
Restored /path/to/mcp.NET/src/Mcp.SqlApiServer/Mcp.SqlApiServer.csproj
Restore succeeded.
```

**1.4 Build the Project**
```bash
dotnet build
```

**Expected Output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**✅ Verification:**
```bash
# Check if build artifacts exist
ls src/Mcp.SqlApiServer/bin/Debug/net8.0/
# Should contain: Mcp.SqlApiServer.dll, Mcp.SqlApiServer.exe (Windows), etc.
```

### Step 2: Configure SQL Server

Choose one of the following configuration methods:

#### Option A: Using User Secrets (Recommended for Development)

**2.1 Initialize User Secrets**
```bash
cd src/Mcp.SqlApiServer
dotnet user-secrets init
```

**2.2 Set Connection String**
```bash
# For local SQL Server
dotnet user-secrets set "Sql:ConnectionString" "Server=localhost;Database=TestDb;User Id=sa;Password=YourPassword;Encrypt=False;TrustServerCertificate=True"

# For Azure SQL
dotnet user-secrets set "Sql:ConnectionString" "Server=tcp:yourserver.database.windows.net,1433;Database=TestDb;User Id=username;Password=password;Encrypt=True"
```

**2.3 Return to Root Directory**
```bash
cd ../..
```

#### Option B: Using Docker SQL Server (Quickest)

**2.1 Start SQL Server Container**
```bash
docker run -e "ACCEPT_EULA=Y" \
  -e "SA_PASSWORD=YourStrong@Passw0rd123" \
  -p 1433:1433 \
  --name mcp-sqlserver \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

**2.2 Wait for SQL Server to Start**
```bash
# Wait 10-15 seconds for SQL Server initialization
sleep 15
```

**2.3 Configure Connection String**
```bash
cd src/Mcp.SqlApiServer
dotnet user-secrets set "Sql:ConnectionString" "Server=localhost,1433;Database=master;User Id=sa;Password=YourStrong@Passw0rd123;Encrypt=False;TrustServerCertificate=True"
cd ../..
```

#### Option C: Using Environment Variables

**2.1 Set Environment Variable**
```bash
# Linux/macOS
export Sql__ConnectionString="Server=localhost;Database=TestDb;User Id=sa;Password=YourPassword;Encrypt=False"

# Windows PowerShell
$env:Sql__ConnectionString="Server=localhost;Database=TestDb;User Id=sa;Password=YourPassword;Encrypt=False"

# Windows CMD
set Sql__ConnectionString=Server=localhost;Database=TestDb;User Id=sa;Password=YourPassword;Encrypt=False
```

**✅ Verification:**
```bash
# Test SQL connection (will try to start server)
dotnet run --project src/Mcp.SqlApiServer http
# If connection string is valid, server will start
# Press Ctrl+C to stop
```

### Step 3: Configure HTTP Security

**3.1 Open Configuration File**
```bash
# Edit the development settings
nano src/Mcp.SqlApiServer/appsettings.Development.json
# Or use your preferred editor: code, vim, etc.
```

**3.2 Add Allowed Hosts**

Edit the `HttpTool` section to include hosts you want to allow:

```json
{
  "HttpTool": {
    "AllowedHosts": [
      "api.github.com",
      "jsonplaceholder.typicode.com",
      "httpbin.org",
      "localhost",
      "127.0.0.1"
    ],
    "DefaultTimeoutSeconds": 30,
    "MaxTimeoutSeconds": 120,
    "AllowAllHosts": false
  }
}
```

**3.3 Security Notes**

⚠️ **Important Security Settings:**
- `AllowAllHosts: false` - Keep disabled in production
- Only add trusted hosts to `AllowedHosts`
- Hosts are case-insensitive
- Subdomains are not automatically included

**✅ Verification:**
```bash
# Check configuration file syntax
cat src/Mcp.SqlApiServer/appsettings.Development.json | grep -A 5 "HttpTool"
# Should display your HttpTool configuration
```

### Step 4: Run the Server

#### Running in Stdio Mode (Default - Production)

**4.1 Start the Server**
```bash
dotnet run --project src/Mcp.SqlApiServer
```

**4.2 Expected Output**
```
info: Program[0]
      MCP SQL/API Server v1.0.0
info: Program[0]
      Transport mode: STDIO (standard MCP protocol)
info: Program[0]
      Tip: Run with 'http' argument or MCP_TRANSPORT_MODE=http for HTTP mode
info: Mcp.SqlApiServer.Infrastructure.McpServer[0]
      MCP Server starting. Reading from stdin...
```

**4.3 Server is Ready**

The server is now waiting for JSON-RPC input on stdin. Keep this terminal open.

**✅ Test Connection (in another terminal):**
```bash
# Send initialize request
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | dotnet run --project src/Mcp.SqlApiServer

# Expected: JSON response with server info
```

#### Running in HTTP Mode (Testing/Development)

**4.1 Start HTTP Server**
```bash
dotnet run --project src/Mcp.SqlApiServer http
```

**4.2 Expected Output**
```
info: Program[0]
      Starting MCP SQL/API Server in HTTP mode on port 5000
info: Program[0]
      Endpoints:
info: Program[0]
        - GET  /tools       - List all available tools
info: Program[0]
        - GET  /health      - Health check
info: Program[0]
        - POST /mcp         - MCP Inspector compatible endpoint
info: Program[0]
        - POST /tools/{name} - Execute specific tool
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5000
```

**4.3 Custom Port (Optional)**
```bash
# Set custom port
export MCP_HTTP_PORT=8080
dotnet run --project src/Mcp.SqlApiServer http
```

**✅ Verification:**
```bash
# Test health endpoint
curl http://localhost:5000/health

# Expected output:
# {
#   "status": "healthy",
#   "server": "mcp-sqlapi-server",
#   "version": "1.0.0",
#   "timestamp": "2024-01-15T10:30:00Z"
# }
```

---

## Testing Guide

Complete step-by-step guide to test all server capabilities.

### Testing with HTTP Mode

This is the recommended approach for initial testing and development.

#### Test 1: Server Health Check

**Purpose:** Verify server is running correctly

**Steps:**
```bash
# 1. Start server in HTTP mode (if not already running)
dotnet run --project src/Mcp.SqlApiServer http

# 2. In another terminal, check health
curl http://localhost:5000/health
```

**Expected Result:**
```json
{
  "status": "healthy",
  "server": "mcp-sqlapi-server",
  "version": "1.0.0",
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

**✅ Success Criteria:** Status is "healthy"

#### Test 2: List Available Tools

**Purpose:** Verify all tools are registered

**Steps:**
```bash
curl http://localhost:5000/tools | jq
```

**Expected Result:**
```json
{
  "tools": [
    {
      "name": "http.call",
      "description": "Make HTTP requests to external APIs...",
      "inputSchema": { ... }
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

**✅ Success Criteria:** Count is 3, all tool names are present

#### Test 3: HTTP API Tool

**Purpose:** Test external HTTP requests

**3.1 Simple GET Request:**
```bash
curl -X POST http://localhost:5000/tools/http.call \
  -H "Content-Type: application/json" \
  -d '{
    "method": "GET",
    "url": "https://api.github.com/users/octocat"
  }' | jq
```

**Expected Result:**
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

**✅ Success Criteria:** statusCode is 200

**3.2 GET with Query Parameters:**
```bash
curl -X POST http://localhost:5000/tools/http.call \
  -H "Content-Type: application/json" \
  -d '{
    "method": "GET",
    "url": "https://api.github.com/users/octocat/repos",
    "query": {
      "per_page": 3,
      "sort": "updated"
    }
  }' | jq '.statusCode'
```

**Expected Result:** `200`

**3.3 POST with JSON Body:**
```bash
curl -X POST http://localhost:5000/tools/http.call \
  -H "Content-Type: application/json" \
  -d '{
    "method": "POST",
    "url": "https://httpbin.org/post",
    "body": {
      "name": "Test User",
      "email": "test@example.com"
    }
  }' | jq '.statusCode'
```

**Expected Result:** `200`

**3.4 Test Host Blocking (Security):**
```bash
curl -X POST http://localhost:5000/tools/http.call \
  -H "Content-Type: application/json" \
  -d '{
    "method": "GET",
    "url": "https://blocked-site.com/api"
  }' | jq
```

**Expected Result:**
```json
{
  "error": "Host 'blocked-site.com' is not in the allowed hosts list..."
}
```

**✅ Success Criteria:** Request is blocked with appropriate error message

#### Test 4: SQL Query Tool

**Purpose:** Test database SELECT operations

**4.1 Simple Query:**
```bash
curl -X POST http://localhost:5000/tools/sql.query \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "SELECT @@VERSION AS SqlVersion, GETDATE() AS CurrentTime"
  }' | jq
```

**Expected Result:**
```json
{
  "rows": [
    {
      "SqlVersion": "Microsoft SQL Server 2022...",
      "CurrentTime": "2024-01-15T10:30:00"
    }
  ],
  "rowCount": 1
}
```

**✅ Success Criteria:** Returns server version and timestamp

**4.2 Parameterized Query:**

First, create a test table (using sql.execute):
```bash
# Create test table
curl -X POST http://localhost:5000/tools/sql.execute \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '\''TestUsers'\'') CREATE TABLE TestUsers (Id INT PRIMARY KEY IDENTITY, Name NVARCHAR(100), Status NVARCHAR(50))"
  }'

# Insert test data
curl -X POST http://localhost:5000/tools/sql.execute \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "INSERT INTO TestUsers (Name, Status) VALUES (@Name, @Status)",
    "parameters": {
      "@Name": "John Doe",
      "@Status": "active"
    }
  }'
```

Then query with parameters:
```bash
curl -X POST http://localhost:5000/tools/sql.query \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "SELECT * FROM TestUsers WHERE Status = @Status",
    "parameters": {
      "@Status": "active"
    }
  }' | jq
```

**Expected Result:**
```json
{
  "rows": [
    {
      "Id": 1,
      "Name": "John Doe",
      "Status": "active"
    }
  ],
  "rowCount": 1
}
```

**4.3 Test Security - Block Non-SELECT:**
```bash
curl -X POST http://localhost:5000/tools/sql.query \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "DELETE FROM TestUsers"
  }' | jq
```

**Expected Result:**
```json
{
  "error": "sql.query only accepts SELECT statements. Use sql.execute for INSERT/UPDATE/DELETE."
}
```

**✅ Success Criteria:** DELETE is blocked with appropriate error

**4.4 Test Security - Block DDL:**
```bash
curl -X POST http://localhost:5000/tools/sql.query \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "DROP TABLE TestUsers"
  }' | jq
```

**Expected Result:**
```json
{
  "error": "DDL operations (DROP, TRUNCATE, ALTER, CREATE, etc.) are not allowed."
}
```

**✅ Success Criteria:** DROP is blocked

#### Test 5: SQL Execute Tool

**Purpose:** Test database modification operations

**5.1 INSERT:**
```bash
curl -X POST http://localhost:5000/tools/sql.execute \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "INSERT INTO TestUsers (Name, Status) VALUES (@Name, @Status)",
    "parameters": {
      "@Name": "Jane Smith",
      "@Status": "pending"
    }
  }' | jq
```

**Expected Result:**
```json
{
  "rowsAffected": 1
}
```

**5.2 UPDATE:**
```bash
curl -X POST http://localhost:5000/tools/sql.execute \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "UPDATE TestUsers SET Status = @NewStatus WHERE Name = @Name",
    "parameters": {
      "@NewStatus": "active",
      "@Name": "Jane Smith"
    }
  }' | jq
```

**Expected Result:**
```json
{
  "rowsAffected": 1
}
```

**5.3 DELETE:**
```bash
curl -X POST http://localhost:5000/tools/sql.execute \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "DELETE FROM TestUsers WHERE Status = @Status",
    "parameters": {
      "@Status": "pending"
    }
  }' | jq
```

**Expected Result:**
```json
{
  "rowsAffected": 0
}
```
(0 because we just updated Jane to 'active')

**✅ Success Criteria:** All operations complete successfully

### Testing with MCP Inspector

MCP Inspector provides a visual interface for testing.

#### Method 1: HTTP Connection (Recommended)

**Step 1: Start Server**
```bash
dotnet run --project src/Mcp.SqlApiServer http
```

**Step 2: Open MCP Inspector**
- Visit: https://inspector.modelcontextprotocol.io
- Or run locally: `npx @modelcontextprotocol/inspector`

**Step 3: Connect**
1. Select "HTTP Transport" from dropdown
2. Enter URL: `http://localhost:5000/mcp`
3. Click "Connect"

**Step 4: Expected State**
- Connection status: ✅ Connected
- Server info displayed: "mcp-sqlapi-server v1.0.0"
- Tools list shows 3 tools

**Step 5: Test a Tool**
1. Click on "http.call" in the tools list
2. Fill in parameters:
   - method: `GET`
   - url: `https://api.github.com/users/octocat`
3. Click "Execute"
4. View response in the output panel

**✅ Success Criteria:** Response displays with status 200

#### Method 2: Stdio Connection

**Step 1: Run Inspector with Stdio**
```bash
npx @modelcontextprotocol/inspector dotnet run --project src/Mcp.SqlApiServer
```

**Step 2: Expected Output**
- Inspector opens in browser
- Server automatically connects
- Tools are listed

**Step 3: Test Tools**
Same as Method 1, steps 4-5

### Testing Individual Tools

Complete test suite for each tool:

#### HTTP Call Tool Test Suite

**Test 1: Basic GET**
```bash
curl -X POST http://localhost:5000/tools/http.call \
  -H "Content-Type: application/json" \
  -d '{"method":"GET","url":"https://httpbin.org/get"}' \
  | jq '.statusCode'
# Expected: 200
```

**Test 2: POST with Body**
```bash
curl -X POST http://localhost:5000/tools/http.call \
  -H "Content-Type: application/json" \
  -d '{
    "method": "POST",
    "url": "https://httpbin.org/post",
    "body": {"test": "data"}
  }' | jq '.statusCode'
# Expected: 200
```

**Test 3: Custom Headers**
```bash
curl -X POST http://localhost:5000/tools/http.call \
  -H "Content-Type: application/json" \
  -d '{
    "method": "GET",
    "url": "https://httpbin.org/headers",
    "headers": {
      "X-Custom-Header": "TestValue",
      "Authorization": "Bearer test-token"
    }
  }' | jq
# Expected: Headers should appear in response
```

**Test 4: Timeout**
```bash
curl -X POST http://localhost:5000/tools/http.call \
  -H "Content-Type: application/json" \
  -d '{
    "method": "GET",
    "url": "https://httpbin.org/delay/2",
    "timeoutSeconds": 5
  }' | jq '.statusCode'
# Expected: 200
```

#### SQL Query Tool Test Suite

**Test 1: System Info**
```bash
curl -X POST http://localhost:5000/tools/sql.query \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName"
  }' | jq '.rows'
# Expected: Server and database information
```

**Test 2: Multiple Rows**
```bash
curl -X POST http://localhost:5000/tools/sql.query \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "SELECT TOP 5 name, type_desc FROM sys.objects ORDER BY name"
  }' | jq '.rowCount'
# Expected: 5
```

**Test 3: NULL Values**
```bash
curl -X POST http://localhost:5000/tools/sql.query \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "SELECT NULL AS NullValue, '\''Text'\'' AS TextValue"
  }' | jq
# Expected: Properly handle NULL
```

#### SQL Execute Tool Test Suite

**Test 1: Create Table**
```bash
curl -X POST http://localhost:5000/tools/sql.execute \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '\''TestLog'\'') CREATE TABLE TestLog (Id INT IDENTITY, Message NVARCHAR(MAX), CreatedAt DATETIME DEFAULT GETDATE())"
  }' | jq
# Expected: rowsAffected: -1 (DDL command)
```

**Test 2: Bulk Insert**
```bash
# Insert multiple records
for i in {1..5}; do
  curl -X POST http://localhost:5000/tools/sql.execute \
    -H "Content-Type: application/json" \
    -d "{
      \"sql\": \"INSERT INTO TestLog (Message) VALUES (@Msg)\",
      \"parameters\": {
        \"@Msg\": \"Test message $i\"
      }
    }"
done
```

**Test 3: Verify**
```bash
curl -X POST http://localhost:5000/tools/sql.query \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "SELECT COUNT(*) AS TotalRecords FROM TestLog"
  }' | jq
# Expected: TotalRecords: 5
```

---

## Integration

### Claude Desktop Integration

Enable Claude Desktop to use this MCP server for database and API access.

**Step 1: Locate Configuration File**

```bash
# macOS
open ~/Library/Application\ Support/Claude/

# Windows
explorer %APPDATA%\Claude\

# Linux
cd ~/.config/Claude/
```

**Step 2: Edit Configuration**

Open or create `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "sql-api-server": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/mcp.NET/src/Mcp.SqlApiServer"
      ],
      "env": {
        "Sql__ConnectionString": "Server=localhost;Database=MyDb;User Id=sa;Password=YourPassword;Encrypt=False;TrustServerCertificate=True"
      }
    }
  }
}
```

**Step 3: Restart Claude Desktop**

**Step 4: Test Integration**

In Claude Desktop chat:
```
Can you query the SQL Server to show me the current date and time?
```

Claude should execute:
```sql
SELECT GETDATE() AS CurrentDateTime
```

### Mini-AGI Backend Integration

Integrate with your AI agent orchestration system.

See complete integration guide in the [README - Mini-AGI Integration Section](#usage-with-mini-agi-backend) or visit the [mini-AGI_Backend repository](https://github.com/b9b4ymiN/mini-AGI_Backend).

**Quick Summary:**
1. Build MCP server: `dotnet publish -c Release -o ./publish`
2. Create Python bridge adapter (provided in README)
3. Register tools in mini-AGI tool registry
4. Configure environment variables
5. Start mini-AGI backend

---

## Configuration

### SQL Server Options

Edit `appsettings.json`:

```json
{
  "Sql": {
    "ConnectionString": "Server=localhost;Database=MyDb;...",
    "BlockDdlOperations": true,
    "MaxRowsReturned": 10000,
    "QueryTimeoutSeconds": 30
  }
}
```

| Setting | Description | Default | Recommended |
|---------|-------------|---------|-------------|
| `ConnectionString` | SQL Server connection string | (required) | Use user-secrets |
| `BlockDdlOperations` | Block DROP, TRUNCATE, ALTER | `true` | `true` (production) |
| `MaxRowsReturned` | Limit rows (0=unlimited) | `10000` | `1000-10000` |
| `QueryTimeoutSeconds` | Query execution timeout | `30` | `30-60` |

### HTTP Tool Options

```json
{
  "HttpTool": {
    "AllowedHosts": ["api.github.com", "httpbin.org"],
    "DefaultTimeoutSeconds": 30,
    "MaxTimeoutSeconds": 120,
    "AllowAllHosts": false
  }
}
```

| Setting | Description | Default | Recommended |
|---------|-------------|---------|-------------|
| `AllowedHosts` | Permitted hostnames | `[]` | List specific domains |
| `DefaultTimeoutSeconds` | Default request timeout | `30` | `15-30` |
| `MaxTimeoutSeconds` | Maximum timeout allowed | `120` | `60-120` |
| `AllowAllHosts` | Disable host filtering | `false` | `false` (always) |

---

## API Reference

### Tool: http.call

**Purpose:** Execute HTTP requests to external APIs

**Input Schema:**
```typescript
{
  method: string;          // HTTP method (GET, POST, etc.)
  url: string;             // Full URL
  headers?: object;        // Optional headers
  query?: object;          // Optional query parameters
  body?: object;           // Optional JSON body
  timeoutSeconds?: number; // Optional timeout override
}
```

**Output Schema:**
```typescript
{
  statusCode: number;           // HTTP status code
  headers: Record<string, string>; // Response headers
  bodyText: string;             // Response body as text
}
```

**Example:**
```json
{
  "method": "GET",
  "url": "https://api.github.com/users/octocat",
  "headers": {
    "Authorization": "Bearer ghp_xxxxxxxxxxxx"
  },
  "query": {
    "per_page": 10
  }
}
```

### Tool: sql.query

**Purpose:** Execute SELECT queries on SQL Server

**Input Schema:**
```typescript
{
  sql: string;                         // SELECT statement
  parameters?: Record<string, any>;    // Named parameters
}
```

**Output Schema:**
```typescript
{
  rows: Array<Record<string, any>>;  // Query results
  rowCount: number;                   // Number of rows
}
```

**Example:**
```json
{
  "sql": "SELECT * FROM Users WHERE Status = @Status AND CreatedDate > @Date",
  "parameters": {
    "@Status": "active",
    "@Date": "2024-01-01"
  }
}
```

### Tool: sql.execute

**Purpose:** Execute INSERT, UPDATE, DELETE on SQL Server

**Input Schema:**
```typescript
{
  sql: string;                         // DML statement
  parameters?: Record<string, any>;    // Named parameters
}
```

**Output Schema:**
```typescript
{
  rowsAffected: number;  // Number of rows modified
}
```

**Example:**
```json
{
  "sql": "UPDATE Users SET LastLogin = GETDATE() WHERE Id = @UserId",
  "parameters": {
    "@UserId": 123
  }
}
```

---

## Security

### Security Best Practices

#### 1. Connection Strings
- ✅ **DO**: Use user-secrets or environment variables
- ❌ **DON'T**: Commit connection strings to source control
- ✅ **DO**: Use encrypted connections in production (`Encrypt=True`)
- ❌ **DON'T**: Use `TrustServerCertificate=True` in production

#### 2. HTTP Allowlist
- ✅ **DO**: Maintain a strict allowlist of permitted hosts
- ❌ **DON'T**: Enable `AllowAllHosts` in production
- ✅ **DO**: Use specific domain names (not wildcards)
- ❌ **DON'T**: Allow access to internal networks

#### 3. SQL Operations
- ✅ **DO**: Keep `BlockDdlOperations` enabled
- ❌ **DON'T**: Allow DDL in production environments
- ✅ **DO**: Always use parameterized queries
- ❌ **DON'T**: Build SQL with string concatenation

#### 4. Network Security
- ✅ **DO**: Use stdio transport in production
- ❌ **DON'T**: Expose HTTP mode to public internet
- ✅ **DO**: Use HTTPS for all external API calls
- ❌ **DON'T**: Accept self-signed certificates without validation

### Security Checklist

Before deploying to production:

- [ ] Connection strings use encrypted connections
- [ ] Connection strings stored in secure secret management
- [ ] `BlockDdlOperations` is `true`
- [ ] `AllowAllHosts` is `false`
- [ ] Specific hosts added to `AllowedHosts`
- [ ] Query timeout configured appropriately
- [ ] Max rows limit configured
- [ ] Logs reviewed for sensitive data exposure
- [ ] HTTP mode disabled (stdio only)
- [ ] Server runs with least-privilege account

---

## Troubleshooting

### Common Issues

#### Issue 1: "SQL connection string is not configured"

**Symptoms:**
```
ERROR: SQL connection string is not configured.
Please set Sql:ConnectionString in appsettings.json or user secrets.
```

**Solutions:**
1. **Set user secrets:**
   ```bash
   cd src/Mcp.SqlApiServer
   dotnet user-secrets set "Sql:ConnectionString" "Server=..."
   ```

2. **Set environment variable:**
   ```bash
   export Sql__ConnectionString="Server=..."
   ```

3. **Check appsettings.json:**
   ```bash
   cat src/Mcp.SqlApiServer/appsettings.Development.json | grep ConnectionString
   ```

#### Issue 2: "Host 'example.com' is not in allowed hosts list"

**Symptoms:**
```json
{
  "error": "Host 'example.com' is not in the allowed hosts list. Allowed hosts: api.github.com, httpbin.org"
}
```

**Solutions:**
1. **Add host to configuration:**
   ```json
   {
     "HttpTool": {
       "AllowedHosts": [
         "api.github.com",
         "httpbin.org",
         "example.com"  ← Add this
       ]
     }
   }
   ```

2. **For testing only:**
   ```json
   {
     "HttpTool": {
       "AllowAllHosts": true  ⚠️ Development only!
     }
   }
   ```

#### Issue 3: "DDL operations are not allowed"

**Symptoms:**
```json
{
  "error": "DDL operations (DROP, TRUNCATE, ALTER, CREATE, etc.) are not allowed."
}
```

**Explanation:** This is a security feature to prevent accidental schema changes.

**Solutions:**
1. **For development/testing:**
   ```json
   {
     "Sql": {
       "BlockDdlOperations": false  ⚠️ Use carefully
     }
   }
   ```

2. **Use database admin tools** for DDL operations instead

#### Issue 4: Connection Timeout

**Symptoms:**
```
System.Data.SqlClient.SqlException: Timeout expired
```

**Solutions:**
1. **Increase query timeout:**
   ```json
   {
     "Sql": {
       "QueryTimeoutSeconds": 60
     }
   }
   ```

2. **Check SQL Server performance:**
   ```sql
   -- Check long-running queries
   SELECT * FROM sys.dm_exec_requests WHERE status = 'running'
   ```

3. **Optimize query:**
   - Add appropriate indexes
   - Reduce data returned
   - Use WHERE clauses effectively

#### Issue 5: HTTP Mode Not Starting

**Symptoms:**
```
Failed to bind to address http://0.0.0.0:5000
```

**Solutions:**
1. **Port already in use:**
   ```bash
   # Find process using port 5000
   lsof -i :5000  # macOS/Linux
   netstat -ano | findstr :5000  # Windows

   # Use different port
   export MCP_HTTP_PORT=8080
   dotnet run --project src/Mcp.SqlApiServer http
   ```

2. **Permission denied:**
   ```bash
   # Use port > 1024
   export MCP_HTTP_PORT=5000
   ```

#### Issue 6: MCP Inspector Won't Connect

**Symptoms:**
- Inspector shows "Connection failed"
- "Network error" in browser console

**Solutions:**
1. **Verify server is running:**
   ```bash
   curl http://localhost:5000/health
   ```

2. **Check CORS:**
   - HTTP mode automatically enables CORS
   - Try localhost instead of 127.0.0.1

3. **Check firewall:**
   ```bash
   # Temporarily disable firewall (testing only)
   # Or add rule for port 5000
   ```

4. **Use stdio mode instead:**
   ```bash
   npx @modelcontextprotocol/inspector dotnet run --project src/Mcp.SqlApiServer
   ```

### Debug Mode

Enable detailed logging:

```bash
# Set logging level to Debug
export Logging__LogLevel__Default=Debug
dotnet run --project src/Mcp.SqlApiServer http
```

View detailed logs:
```bash
# All logs go to stderr
dotnet run --project src/Mcp.SqlApiServer 2> debug.log
```

---

## Usage with mini-AGI Backend

[Previous mini-AGI integration content remains here - keeping existing detailed integration guide]

---

## Contributing

We welcome contributions! Please see [CLAUDE.md](./CLAUDE.md) for development guidelines.

**Development Setup:**
```bash
git clone https://github.com/b9b4ymiN/mcp.NET.git
cd mcp.NET
dotnet restore
dotnet build
dotnet test
```

**Coding Standards:**
- Follow C# naming conventions
- Use async/await for I/O operations
- Add XML documentation comments
- Write unit tests for new features
- Update README for API changes

## License

[Your License Here]

## Resources

- [MCP Protocol Specification](https://modelcontextprotocol.io/docs)
- [MCP Inspector](https://github.com/modelcontextprotocol/inspector)
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [Dapper ORM](https://github.com/DapperLib/Dapper)
- [SQL Server Documentation](https://learn.microsoft.com/en-us/sql/)

---

**Version:** 1.0.0
**Last Updated:** 2024-01-15
**Maintainer:** [Your Name]

For issues and questions, please visit the [GitHub Issues](https://github.com/b9b4ymiN/mcp.NET/issues) page.
