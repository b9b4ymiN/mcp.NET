# IIS Deployment Guide for MCP SQL/API Server

This guide provides step-by-step instructions for deploying the MCP SQL/API Server to Internet Information Services (IIS) on Windows Server.

## 📋 Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Deployment Steps](#deployment-steps)
  - [Step 1: Publish the Application](#step-1-publish-the-application)
  - [Step 2: Install IIS and Components](#step-2-install-iis-and-components)
  - [Step 3: Configure Application Pool](#step-3-configure-application-pool)
  - [Step 4: Create IIS Website](#step-4-create-iis-website)
  - [Step 5: Configure Environment Variables](#step-5-configure-environment-variables)
  - [Step 6: Configure HTTPS/SSL](#step-6-configure-httpsssl)
  - [Step 7: Test Deployment](#step-7-test-deployment)
- [Configuration](#configuration)
- [Security Best Practices](#security-best-practices)
- [Performance Tuning](#performance-tuning)
- [Troubleshooting](#troubleshooting)
- [Monitoring and Logging](#monitoring-and-logging)

## Overview

Deploying the MCP server to IIS enables:
- **HTTP Mode Only**: IIS deployment runs the server in HTTP transport mode
- **Enterprise Integration**: Leverage IIS features (load balancing, SSL, logging)
- **Windows Authentication**: Optional integration with Active Directory
- **Centralized Management**: Use IIS Manager for configuration
- **Auto-restart**: IIS can automatically restart crashed applications

**Architecture:**
```
┌─────────────────────┐
│   MCP Clients       │
│ (Browser, Postman)  │
└──────────┬──────────┘
           │ HTTPS
┌──────────▼──────────┐
│  IIS Web Server     │
│  (Windows Server)   │
├─────────────────────┤
│ • Application Pool  │
│ • .NET 8 Runtime    │
│ • URL Rewrite       │
└──────────┬──────────┘
           │
┌──────────▼──────────┐
│  MCP Server (HTTP)  │
│  This Application   │
└─────────────────────┘
```

⚠️ **Important Notes:**
- IIS deployment only supports **HTTP mode** (not stdio mode)
- Suitable for testing, monitoring, and API integration scenarios
- Not recommended for Claude Desktop integration (use stdio mode instead)
- Ideal for mini-AGI Backend or other HTTP-based integrations

## Prerequisites

### System Requirements

| Requirement | Version | Notes |
|-------------|---------|-------|
| Windows Server | 2016+ | Or Windows 10/11 Pro |
| IIS | 10.0+ | With ASP.NET Core Module |
| .NET Runtime | 8.0+ | ASP.NET Core Runtime |
| .NET SDK | 8.0+ | For publishing (dev machine) |
| SQL Server | 2019+ | Optional, if using SQL tools |

### Required Windows Features

Enable these Windows features before proceeding:

**Using Server Manager:**
1. Open **Server Manager**
2. Click **Manage** → **Add Roles and Features**
3. Select **Web Server (IIS)** role
4. Include these features:
   - Web Server → Common HTTP Features → Static Content
   - Web Server → Application Development → ASP.NET 4.8
   - Web Server → Health and Diagnostics → HTTP Logging
   - Web Server → Security → Request Filtering
   - Management Tools → IIS Management Console

**Using PowerShell (Administrator):**
```powershell
# Install IIS
Install-WindowsFeature -Name Web-Server -IncludeManagementTools

# Install required features
Install-WindowsFeature -Name Web-Asp-Net45
Install-WindowsFeature -Name Web-Http-Logging
Install-WindowsFeature -Name Web-Request-Monitor
Install-WindowsFeature -Name Web-Filtering
```

### Install .NET 8 Hosting Bundle

**Required for IIS to host .NET 8 applications**

**Step 1: Download Hosting Bundle**
```powershell
# Download .NET 8 Hosting Bundle
# Visit: https://dotnet.microsoft.com/download/dotnet/8.0
# Download: "Hosting Bundle" installer

# Or using PowerShell
$url = "https://download.visualstudio.microsoft.com/download/pr/.../.../dotnet-hosting-8.0.x-win.exe"
Invoke-WebRequest -Uri $url -OutFile "$env:TEMP\dotnet-hosting.exe"
```

**Step 2: Install Hosting Bundle**
```powershell
# Run installer
Start-Process -FilePath "$env:TEMP\dotnet-hosting.exe" -ArgumentList "/quiet /install" -Wait

# Verify installation
dotnet --list-runtimes
# Should show: Microsoft.AspNetCore.App 8.0.x
```

**Step 3: Restart IIS**
```powershell
net stop was /y
net start w3svc
```

**✅ Verification:**
```powershell
# Check .NET version
dotnet --version
# Expected: 8.0.x or higher

# Check installed runtimes
dotnet --list-runtimes
# Should include:
# Microsoft.AspNetCore.App 8.0.x
# Microsoft.NETCore.App 8.0.x
```

## Deployment Steps

### Step 1: Publish the Application

Build and publish the application for IIS deployment.

#### 1.1 Publish Using Command Line

**On your development machine:**

```powershell
# Navigate to repository
cd C:\path\to\mcp.NET

# Publish for Windows x64 (self-contained)
dotnet publish src/Mcp.SqlApiServer/Mcp.SqlApiServer.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o "C:\Publish\McpSqlApiServer"

# Or framework-dependent (smaller, requires .NET runtime on server)
dotnet publish src/Mcp.SqlApiServer/Mcp.SqlApiServer.csproj `
  -c Release `
  -o "C:\Publish\McpSqlApiServer"
```

**Expected Output:**
```
Microsoft (R) Build Engine version 17.x.x
...
Mcp.SqlApiServer -> C:\path\to\mcp.NET\src\Mcp.SqlApiServer\bin\Release\net8.0\win-x64\Mcp.SqlApiServer.dll
Mcp.SqlApiServer -> C:\Publish\McpSqlApiServer\
```

#### 1.2 Verify Published Files

**Check publish directory:**
```powershell
dir C:\Publish\McpSqlApiServer
```

**Required files:**
- `Mcp.SqlApiServer.dll` (main assembly)
- `Mcp.SqlApiServer.exe` (if self-contained)
- `appsettings.json`
- `appsettings.Production.json` (create this)
- `web.config` (auto-generated)
- All dependency DLLs

#### 1.3 Create Production Configuration

**Create `appsettings.Production.json`:**

```powershell
# In publish directory
cd C:\Publish\McpSqlApiServer
notepad appsettings.Production.json
```

**Content:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "AllowedHosts": "*",
  "Sql": {
    "ConnectionString": "",
    "BlockDdlOperations": true,
    "MaxRowsReturned": 1000,
    "QueryTimeoutSeconds": 30
  },
  "HttpTool": {
    "AllowedHosts": [
      "api.github.com",
      "jsonplaceholder.typicode.com"
    ],
    "DefaultTimeoutSeconds": 30,
    "MaxTimeoutSeconds": 60,
    "AllowAllHosts": false
  }
}
```

⚠️ **Note:** Connection string will be set via environment variables for security.

#### 1.4 Transfer to Server

**Copy published files to Windows Server:**

```powershell
# Option 1: Using RDP (copy/paste)
# Copy C:\Publish\McpSqlApiServer to clipboard
# Paste on server: C:\inetpub\wwwroot\McpSqlApiServer

# Option 2: Using network share
Copy-Item -Path "C:\Publish\McpSqlApiServer" `
  -Destination "\\SERVER\C$\inetpub\wwwroot\" `
  -Recurse

# Option 3: Using PowerShell remoting
$session = New-PSSession -ComputerName SERVER
Copy-Item -Path "C:\Publish\McpSqlApiServer" `
  -Destination "C:\inetpub\wwwroot\" `
  -ToSession $session `
  -Recurse
```

**Recommended server path:**
```
C:\inetpub\wwwroot\McpSqlApiServer\
```

### Step 2: Install IIS and Components

If not already installed, set up IIS and required components.

#### 2.1 Verify IIS Installation

**Open IIS Manager:**
```powershell
# Launch IIS Manager
inetmgr

# Or from Run dialog (Win+R)
inetmgr
```

**Check IIS version:**
```powershell
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\InetStp" | Select-Object VersionString
# Expected: 10.0 or higher
```

#### 2.2 Install URL Rewrite Module (Optional but Recommended)

**Download and install:**
```powershell
# Download URL Rewrite Module
$url = "https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi"
Invoke-WebRequest -Uri $url -OutFile "$env:TEMP\urlrewrite.msi"

# Install
Start-Process msiexec.exe -ArgumentList "/i $env:TEMP\urlrewrite.msi /quiet" -Wait
```

### Step 3: Configure Application Pool

Create a dedicated application pool for the MCP server.

#### 3.1 Create Application Pool

**Using IIS Manager:**

1. Open **IIS Manager**
2. Expand server node
3. Right-click **Application Pools** → **Add Application Pool**
4. Configure:
   - **Name:** `McpSqlApiServerPool`
   - **.NET CLR Version:** No Managed Code
   - **Managed pipeline mode:** Integrated
   - **Start application pool immediately:** ✅ Checked
5. Click **OK**

**Using PowerShell:**
```powershell
Import-Module WebAdministration

# Create application pool
New-WebAppPool -Name "McpSqlApiServerPool"

# Configure application pool
Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" -Name "managedPipelineMode" -Value "Integrated"
```

#### 3.2 Configure Application Pool Settings

**Using IIS Manager:**

1. Click **Application Pools**
2. Select **McpSqlApiServerPool**
3. Click **Advanced Settings** (right panel)
4. Configure:

**Process Model:**
- **Identity:** `ApplicationPoolIdentity` (recommended)
- **Idle Time-out:** 20 minutes
- **Maximum Worker Processes:** 1
- **Load User Profile:** True

**Recycling:**
- **Regular Time Interval:** 1740 (29 hours)
- **Specific Times:** (leave empty)
- **Request Limit:** 0
- **Virtual Memory Limit:** 0

**Rapid-Fail Protection:**
- **Enabled:** True
- **Maximum Failures:** 5
- **Failure Interval:** 5 minutes
- **Shutdown Time Limit:** 90 seconds

**Using PowerShell:**
```powershell
# Configure process model
Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" -Name "processModel.idleTimeout" -Value "00:20:00"
Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" -Name "processModel.loadUserProfile" -Value $true

# Configure recycling
Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" -Name "recycling.periodicRestart.time" -Value "1.05:00:00"

# Start application pool
Start-WebAppPool -Name "McpSqlApiServerPool"
```

**✅ Verification:**
```powershell
Get-WebAppPoolState -Name "McpSqlApiServerPool"
# Expected: Value = Started
```

### Step 4: Create IIS Website

Configure the IIS website to host the MCP server.

#### 4.1 Create Website

**Using IIS Manager:**

1. Right-click **Sites** → **Add Website**
2. Configure:
   - **Site name:** `McpSqlApiServer`
   - **Application pool:** `McpSqlApiServerPool`
   - **Physical path:** `C:\inetpub\wwwroot\McpSqlApiServer`
   - **Binding:**
     - Type: `http`
     - IP address: `All Unassigned`
     - Port: `5000`
     - Host name: (leave empty or specify: `mcp-server.yourdomain.com`)
3. Click **OK**

**Using PowerShell:**
```powershell
# Create website
New-Website -Name "McpSqlApiServer" `
  -ApplicationPool "McpSqlApiServerPool" `
  -PhysicalPath "C:\inetpub\wwwroot\McpSqlApiServer" `
  -Port 5000

# Or with hostname binding
New-Website -Name "McpSqlApiServer" `
  -ApplicationPool "McpSqlApiServerPool" `
  -PhysicalPath "C:\inetpub\wwwroot\McpSqlApiServer" `
  -Port 80 `
  -HostHeader "mcp-server.yourdomain.com"
```

#### 4.2 Configure Website Settings

**Enable Directory Browsing (Optional):**
```powershell
Set-WebConfigurationProperty -Filter "/system.webServer/directoryBrowse" `
  -Name "enabled" `
  -Value $false `
  -PSPath "IIS:\Sites\McpSqlApiServer"
```

**Set Default Document:**
```powershell
# Not needed for ASP.NET Core, but good practice
Clear-WebConfiguration -Filter "/system.webServer/defaultDocument/files" -PSPath "IIS:\Sites\McpSqlApiServer"
Add-WebConfiguration -Filter "/system.webServer/defaultDocument/files" `
  -Value @{value='index.html'} `
  -PSPath "IIS:\Sites\McpSqlApiServer"
```

**✅ Verification:**
```powershell
Get-Website -Name "McpSqlApiServer"
# Check State = Started
```

### Step 5: Configure Environment Variables

Set environment variables for configuration (recommended for security).

#### 5.1 Set Application Pool Environment Variables

**Using IIS Manager:**

1. Select **Application Pools**
2. Select **McpSqlApiServerPool**
3. Click **Advanced Settings**
4. Scroll to **Environment Variables** section
5. Click **...** button
6. Add variables (see below)

**Note:** If "Environment Variables" is not visible, use PowerShell method.

#### 5.2 Using PowerShell to Set Environment Variables

**PowerShell script:**

```powershell
# Import required module
Import-Module WebAdministration

# Set transport mode to HTTP
[Environment]::SetEnvironmentVariable(
    "MCP_TRANSPORT_MODE",
    "http",
    "Machine"
)

# Set SQL connection string
[Environment]::SetEnvironmentVariable(
    "Sql__ConnectionString",
    "Server=localhost;Database=MyDb;User Id=sa;Password=YourSecurePassword;Encrypt=True;TrustServerCertificate=False",
    "Machine"
)

# Set HTTP port (optional, default is 5000)
[Environment]::SetEnvironmentVariable(
    "MCP_HTTP_PORT",
    "5000",
    "Machine"
)

# Set allowed hosts (JSON array format)
# Note: For multiple values, configure in appsettings.Production.json instead

# Set ASPNETCORE environment
[Environment]::SetEnvironmentVariable(
    "ASPNETCORE_ENVIRONMENT",
    "Production",
    "Machine"
)

# Restart IIS to apply changes
net stop was /y
net start w3svc
```

#### 5.3 Alternative: Using web.config

**Edit `web.config` in application directory:**

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\Mcp.SqlApiServer.dll http"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
          <environmentVariable name="MCP_TRANSPORT_MODE" value="http" />
          <environmentVariable name="MCP_HTTP_PORT" value="5000" />
          <environmentVariable name="Sql__ConnectionString" value="Server=localhost;Database=MyDb;User Id=sa;Password=YourPassword;Encrypt=True" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

⚠️ **Security Warning:**
- Storing connection strings in web.config is less secure
- Use machine-level environment variables or Azure Key Vault in production

#### 5.4 Create Logs Directory

**Create directory for application logs:**
```powershell
New-Item -Path "C:\inetpub\wwwroot\McpSqlApiServer\logs" -ItemType Directory

# Set permissions
$acl = Get-Acl "C:\inetpub\wwwroot\McpSqlApiServer\logs"
$permission = "IIS AppPool\McpSqlApiServerPool", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl "C:\inetpub\wwwroot\McpSqlApiServer\logs" $acl
```

### Step 6: Configure HTTPS/SSL

Enable HTTPS for secure communication (recommended for production).

#### 6.1 Install SSL Certificate

**Option A: Using Self-Signed Certificate (Development/Testing)**

```powershell
# Create self-signed certificate
$cert = New-SelfSignedCertificate `
  -DnsName "mcp-server.yourdomain.com" `
  -CertStoreLocation "cert:\LocalMachine\My" `
  -NotAfter (Get-Date).AddYears(2)

# Note the thumbprint
$cert.Thumbprint
```

**Option B: Using Commercial Certificate**

1. Purchase SSL certificate from CA (GoDaddy, DigiCert, etc.)
2. Generate CSR from IIS Manager
3. Submit CSR to CA
4. Install received certificate in IIS

**Option C: Using Let's Encrypt (Free)**

```powershell
# Install win-acme client
Invoke-WebRequest -Uri "https://github.com/win-acme/win-acme/releases/latest/download/win-acme.v2.x.x.x64.pluggable.zip" `
  -OutFile "$env:TEMP\win-acme.zip"

# Extract and run
Expand-Archive -Path "$env:TEMP\win-acme.zip" -DestinationPath "C:\win-acme"
cd C:\win-acme
.\wacs.exe

# Follow prompts to create certificate
```

#### 6.2 Bind Certificate to Website

**Using IIS Manager:**

1. Select **McpSqlApiServer** site
2. Click **Bindings** (right panel)
3. Click **Add**
4. Configure:
   - **Type:** https
   - **IP address:** All Unassigned
   - **Port:** 443
   - **SSL certificate:** Select your certificate
5. Click **OK**

**Using PowerShell:**
```powershell
# Bind certificate to website
New-WebBinding -Name "McpSqlApiServer" `
  -Protocol "https" `
  -Port 443 `
  -IPAddress "*"

# Associate certificate
$cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object {$_.Subject -like "*mcp-server*"}
Get-WebBinding -Name "McpSqlApiServer" -Protocol "https" |
  Add-NetIPHttpsCertBinding -CertificateHash $cert.Thumbprint -CertificateStoreName "My"
```

#### 6.3 Configure HTTP to HTTPS Redirect

**Edit web.config:**
```xml
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="HTTP to HTTPS redirect" stopProcessing="true">
          <match url="(.*)" />
          <conditions>
            <add input="{HTTPS}" pattern="off" ignoreCase="true" />
          </conditions>
          <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

### Step 7: Test Deployment

Verify the deployment is working correctly.

#### 7.1 Start Website

**Using IIS Manager:**
1. Select **McpSqlApiServer** site
2. Click **Start** (right panel)

**Using PowerShell:**
```powershell
Start-Website -Name "McpSqlApiServer"
```

#### 7.2 Test Health Endpoint

**From server:**
```powershell
# Test HTTP
Invoke-WebRequest -Uri "http://localhost:5000/health" | Select-Object -ExpandProperty Content

# Test HTTPS (if configured)
Invoke-WebRequest -Uri "https://localhost/health" | Select-Object -ExpandProperty Content
```

**Expected Response:**
```json
{
  "status": "healthy",
  "server": "mcp-sqlapi-server",
  "version": "1.0.0",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

#### 7.3 Test Tools Endpoint

```powershell
Invoke-WebRequest -Uri "http://localhost:5000/tools" |
  ConvertFrom-Json |
  Select-Object -ExpandProperty tools |
  Select-Object name
```

**Expected Output:**
```
name
----
http.call
sql.query
sql.execute
```

#### 7.4 Test from Remote Client

**From another machine:**
```powershell
# Replace SERVER_IP with actual server IP or hostname
Invoke-WebRequest -Uri "http://SERVER_IP:5000/health"

# Or using browser
# Navigate to: http://SERVER_IP:5000/tools
```

**✅ Success Criteria:**
- Health endpoint returns "healthy" status
- Tools endpoint lists 3 tools
- No errors in browser console or IIS logs

## Configuration

### Firewall Configuration

**Open required ports:**

```powershell
# Allow HTTP (port 5000 or 80)
New-NetFirewallRule -DisplayName "MCP Server HTTP" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 5000 `
  -Action Allow

# Allow HTTPS (port 443)
New-NetFirewallRule -DisplayName "MCP Server HTTPS" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 443 `
  -Action Allow
```

### SQL Server Access

**Grant application pool identity access to SQL Server:**

```sql
-- Connect to SQL Server
-- Replace [SERVERNAME] with your server name

USE [master]
GO

-- Create login for IIS AppPool identity
CREATE LOGIN [IIS APPPOOL\McpSqlApiServerPool] FROM WINDOWS
GO

-- Grant access to database
USE [YourDatabase]
GO

CREATE USER [IIS APPPOOL\McpSqlApiServerPool] FOR LOGIN [IIS APPPOOL\McpSqlApiServerPool]
GO

-- Grant necessary permissions
ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\McpSqlApiServerPool]
ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\McpSqlApiServerPool]
GO
```

**Update connection string to use Windows Authentication:**
```
Server=localhost;Database=YourDatabase;Integrated Security=true;TrustServerCertificate=False
```

### Application Settings

**Edit `appsettings.Production.json`:**

```json
{
  "Sql": {
    "ConnectionString": "Server=localhost;Database=Production;Integrated Security=true;",
    "BlockDdlOperations": true,
    "MaxRowsReturned": 1000,
    "QueryTimeoutSeconds": 30
  },
  "HttpTool": {
    "AllowedHosts": [
      "api.yourdomain.com",
      "external-api.com"
    ],
    "DefaultTimeoutSeconds": 30,
    "MaxTimeoutSeconds": 60,
    "AllowAllHosts": false
  }
}
```

## Security Best Practices

### 1. Application Pool Identity

**Recommended:** Use a custom service account instead of ApplicationPoolIdentity for production.

```powershell
# Create service account (in Active Directory)
# Grant minimal permissions needed

# Configure app pool to use custom account
Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" `
  -Name "processModel.identityType" `
  -Value "SpecificUser"

Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" `
  -Name "processModel.userName" `
  -Value "DOMAIN\McpServiceAccount"

Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" `
  -Name "processModel.password" `
  -Value "SecurePassword123!"
```

### 2. Request Filtering

**Configure request limits:**

```powershell
# Set maximum URL length
Set-WebConfigurationProperty -Filter "/system.webServer/security/requestFiltering/requestLimits" `
  -Name "maxUrl" `
  -Value 4096 `
  -PSPath "IIS:\Sites\McpSqlApiServer"

# Set maximum query string length
Set-WebConfigurationProperty -Filter "/system.webServer/security/requestFiltering/requestLimits" `
  -Name "maxQueryString" `
  -Value 2048 `
  -PSPath "IIS:\Sites\McpSqlApiServer"

# Set maximum content length (50 MB)
Set-WebConfigurationProperty -Filter "/system.webServer/security/requestFiltering/requestLimits" `
  -Name "maxAllowedContentLength" `
  -Value 52428800 `
  -PSPath "IIS:\Sites\McpSqlApiServer"
```

### 3. IP Restrictions

**Limit access to specific IP addresses:**

```powershell
# Allow only specific IPs
Add-WebConfigurationProperty -Filter "/system.webServer/security/ipSecurity" `
  -Name "." `
  -Value @{ipAddress='192.168.1.100';allowed='true'} `
  -PSPath "IIS:\Sites\McpSqlApiServer"

# Deny all others
Set-WebConfigurationProperty -Filter "/system.webServer/security/ipSecurity" `
  -Name "allowUnlisted" `
  -Value $false `
  -PSPath "IIS:\Sites\McpSqlApiServer"
```

### 4. Enable HTTPS Only

**Require SSL:**

```powershell
Set-WebConfigurationProperty -Filter "/system.webServer/security/access" `
  -Name "sslFlags" `
  -Value "Ssl" `
  -PSPath "IIS:\Sites\McpSqlApiServer"
```

## Performance Tuning

### Application Pool Tuning

```powershell
# Increase queue length
Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" `
  -Name "queueLength" `
  -Value 5000

# Enable rapid-fail protection
Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" `
  -Name "failure.rapidFailProtection" `
  -Value $true

# Set CPU limit (percentage)
Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" `
  -Name "cpu.limit" `
  -Value 0  # 0 = no limit
```

### Compression

**Enable compression:**

```powershell
# Enable static compression
Set-WebConfigurationProperty -Filter "/system.webServer/urlCompression" `
  -Name "doStaticCompression" `
  -Value $true `
  -PSPath "IIS:\Sites\McpSqlApiServer"

# Enable dynamic compression
Set-WebConfigurationProperty -Filter "/system.webServer/urlCompression" `
  -Name "doDynamicCompression" `
  -Value $true `
  -PSPath "IIS:\Sites\McpSqlApiServer"
```

### Caching

**Configure output caching:**

```xml
<!-- Add to web.config -->
<system.webServer>
  <caching>
    <profiles>
      <add extension=".json" policy="CacheUntilChange" kernelCachePolicy="CacheUntilChange" />
    </profiles>
  </caching>
</system.webServer>
```

## Troubleshooting

### Common Issues

#### Issue 1: HTTP 500.19 - Configuration Error

**Symptoms:**
```
HTTP Error 500.19 - Internal Server Error
The requested page cannot be accessed because the related configuration data is invalid.
```

**Solutions:**

1. **Check web.config syntax:**
   ```powershell
   # Validate XML
   [xml](Get-Content "C:\inetpub\wwwroot\McpSqlApiServer\web.config")
   ```

2. **Install ASP.NET Core Module:**
   ```powershell
   # Reinstall .NET Hosting Bundle
   # Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   ```

3. **Check event viewer:**
   ```powershell
   Get-EventLog -LogName Application -Source "IIS*" -Newest 10
   ```

#### Issue 2: HTTP 500.30 - ASP.NET Core App Failed to Start

**Symptoms:**
```
HTTP Error 500.30 - ASP.NET Core app failed to start
```

**Solutions:**

1. **Check stdout logs:**
   ```powershell
   Get-Content "C:\inetpub\wwwroot\McpSqlApiServer\logs\stdout*.log" | Select-Object -Last 50
   ```

2. **Verify .NET Runtime:**
   ```powershell
   dotnet --list-runtimes
   # Ensure Microsoft.AspNetCore.App 8.0.x is listed
   ```

3. **Test application directly:**
   ```powershell
   cd C:\inetpub\wwwroot\McpSqlApiServer
   dotnet Mcp.SqlApiServer.dll http
   # Check for error messages
   ```

#### Issue 3: Application Pool Crashes

**Symptoms:**
- Application pool stops frequently
- Event ID 5002 in Application log

**Solutions:**

1. **Check Event Viewer:**
   ```powershell
   Get-WinEvent -LogName Application |
     Where-Object {$_.ProviderName -like "*ASP.NET*"} |
     Select-Object -First 10
   ```

2. **Enable process dump:**
   ```powershell
   Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" `
     -Name "failure.autoShutdownExe" `
     -Value "C:\Windows\System32\procdump.exe"
   ```

3. **Increase timeout values:**
   ```powershell
   Set-ItemProperty "IIS:\AppPools\McpSqlApiServerPool" `
     -Name "processModel.shutdownTimeLimit" `
     -Value "00:05:00"
   ```

#### Issue 4: SQL Connection Fails

**Symptoms:**
```
ERROR: SQL connection string is not configured
```

**Solutions:**

1. **Verify environment variable:**
   ```powershell
   [Environment]::GetEnvironmentVariable("Sql__ConnectionString", "Machine")
   ```

2. **Test SQL connection:**
   ```powershell
   # From application directory
   sqlcmd -S localhost -d MyDb -E
   # Should connect successfully
   ```

3. **Check firewall:**
   ```powershell
   Test-NetConnection -ComputerName localhost -Port 1433
   ```

### Enable Debug Logging

**Temporary debugging:**

```powershell
# Stop website
Stop-Website -Name "McpSqlApiServer"

# Set environment variable
[Environment]::SetEnvironmentVariable("Logging__LogLevel__Default", "Debug", "Machine")

# Restart IIS
iisreset

# Start website
Start-Website -Name "McpSqlApiServer"

# Check logs
Get-Content "C:\inetpub\wwwroot\McpSqlApiServer\logs\stdout*.log" -Wait
```

## Monitoring and Logging

### IIS Logging

**Enable detailed logging:**

```powershell
# Enable failed request tracing
Set-WebConfigurationProperty -Filter "/system.webServer/tracing/traceFailedRequests" `
  -Name "enabled" `
  -Value $true `
  -PSPath "IIS:\Sites\McpSqlApiServer"

# Configure what to trace
Add-WebConfigurationProperty -Filter "/system.webServer/tracing/traceFailedRequests" `
  -Name "." `
  -Value @{path='*';statusCodes='400-599'} `
  -PSPath "IIS:\Sites\McpSqlApiServer"
```

### Application Insights (Optional)

**Add Application Insights:**

1. **Install NuGet package** (on dev machine):
   ```powershell
   dotnet add package Microsoft.ApplicationInsights.AspNetCore
   ```

2. **Update Program.cs:**
   ```csharp
   builder.Services.AddApplicationInsightsTelemetry();
   ```

3. **Set instrumentation key** (environment variable):
   ```powershell
   [Environment]::SetEnvironmentVariable(
       "APPLICATIONINSIGHTS_CONNECTION_STRING",
       "InstrumentationKey=your-key-here",
       "Machine"
   )
   ```

### Windows Performance Monitor

**Monitor application performance:**

```powershell
# Create performance counter collector
$collector = New-Object System.Diagnostics.PerformanceCounterCategory
$collector.CategoryName = "ASP.NET Core"

# View available counters
$collector.GetCounters() | Select-Object CounterName
```

## Conclusion

Your MCP SQL/API Server is now deployed to IIS and ready for production use.

**Quick Reference:**
- **Website URL:** `http://your-server:5000` or `https://your-server`
- **Health Check:** `http://your-server:5000/health`
- **Tools List:** `http://your-server:5000/tools`
- **MCP Endpoint:** `http://your-server:5000/mcp`
- **Logs:** `C:\inetpub\wwwroot\McpSqlApiServer\logs\`
- **IIS Manager:** Run `inetmgr`

**Next Steps:**
- Configure monitoring and alerting
- Set up automated backups
- Implement CI/CD pipeline
- Load test the deployment
- Document custom configurations

For issues and support, see:
- [Main README](./README.md)
- [Troubleshooting Guide](./MCP_INSPECTOR.md)
- [GitHub Issues](https://github.com/b9b4ymiN/mcp.NET/issues)
