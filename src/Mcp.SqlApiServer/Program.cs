using Mcp.SqlApiServer.Config;
using Mcp.SqlApiServer.Endpoints;
using Mcp.SqlApiServer.Infrastructure;
using Mcp.SqlApiServer.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Check transport mode: stdio (default for MCP) or http (for testing/monitoring)
var transportMode = args.FirstOrDefault()?.ToLowerInvariant() ??
                    Environment.GetEnvironmentVariable("MCP_TRANSPORT_MODE")?.ToLowerInvariant() ??
                    "stdio";

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();

if (transportMode == "http")
{
    // HTTP mode - for MCP Inspector, testing, and tool discovery
    var builder = WebApplication.CreateBuilder(args);

    // Configure logging
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Information);

    // Add configuration
    builder.Services.Configure<HttpToolOptions>(configuration.GetSection(HttpToolOptions.SectionName));
    builder.Services.Configure<SqlOptions>(configuration.GetSection(SqlOptions.SectionName));

    // Add HttpClient
    builder.Services.AddHttpClient("McpHttpClient", client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "MCP-SqlApi-Server/1.0");
    });

    // Add tool services
    builder.Services.AddSingleton<HttpToolService>();
    builder.Services.AddSingleton<SqlToolService>();

    // Add CORS for MCP Inspector
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // Validate configuration
    var sqlOptions = configuration.GetSection(SqlOptions.SectionName).Get<SqlOptions>();
    if (sqlOptions == null || string.IsNullOrWhiteSpace(sqlOptions.ConnectionString))
    {
        app.Logger.LogWarning("SQL connection string is not configured. SQL tools will not work.");
    }

    app.UseCors();

    // Map MCP HTTP endpoints
    app.MapMcpEndpoints();

    var port = Environment.GetEnvironmentVariable("MCP_HTTP_PORT") ?? "5000";
    app.Logger.LogInformation("Starting MCP SQL/API Server in HTTP mode on port {Port}", port);
    app.Logger.LogInformation("Endpoints:");
    app.Logger.LogInformation("  - GET  /tools       - List all available tools");
    app.Logger.LogInformation("  - GET  /health      - Health check");
    app.Logger.LogInformation("  - POST /mcp         - MCP Inspector compatible endpoint");
    app.Logger.LogInformation("  - POST /tools/{{name}} - Execute specific tool");

    app.Run($"http://0.0.0.0:{port}");
}
else
{
    // Stdio mode - default for MCP protocol (Claude Desktop, etc.)
    var services = new ServiceCollection();

    // Add logging
    services.AddLogging(builder =>
    {
        builder.AddConsole(options =>
        {
            // Log to stderr to avoid interfering with MCP protocol on stdout
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.SetMinimumLevel(LogLevel.Information);
    });

    // Add configuration options
    services.Configure<HttpToolOptions>(configuration.GetSection(HttpToolOptions.SectionName));
    services.Configure<SqlOptions>(configuration.GetSection(SqlOptions.SectionName));

    // Add HttpClient
    services.AddHttpClient("McpHttpClient", client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent", "MCP-SqlApi-Server/1.0");
    });

    // Add tool services
    services.AddSingleton<HttpToolService>();
    services.AddSingleton<SqlToolService>();

    // Add MCP server
    services.AddSingleton<McpServer>();

    var serviceProvider = services.BuildServiceProvider();

    // Validate required configuration
    var sqlOptions = configuration.GetSection(SqlOptions.SectionName).Get<SqlOptions>();
    if (sqlOptions == null || string.IsNullOrWhiteSpace(sqlOptions.ConnectionString))
    {
        Console.Error.WriteLine("ERROR: SQL connection string is not configured.");
        Console.Error.WriteLine("Please set Sql:ConnectionString in appsettings.json or user secrets.");
        return 1;
    }

    // Get logger
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("MCP SQL/API Server v1.0.0");
    logger.LogInformation("Transport mode: STDIO (standard MCP protocol)");
    logger.LogInformation("Tip: Run with 'http' argument or MCP_TRANSPORT_MODE=http for HTTP mode");

    // Setup graceful shutdown
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, e) =>
    {
        logger.LogInformation("Shutdown signal received");
        e.Cancel = true;
        cts.Cancel();
    };

    try
    {
        // Run MCP server
        var mcpServer = serviceProvider.GetRequiredService<McpServer>();
        await mcpServer.RunAsync(cts.Token);

        logger.LogInformation("Server stopped gracefully");
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Fatal error: {Message}", ex.Message);
        return 1;
    }
}
