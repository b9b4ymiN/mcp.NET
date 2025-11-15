using Mcp.SqlApiServer.Config;
using Mcp.SqlApiServer.Infrastructure;
using Mcp.SqlApiServer.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();

// Build service provider
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
logger.LogInformation("Starting server...");

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
