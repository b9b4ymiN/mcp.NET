namespace Mcp.SqlApiServer.Config;

/// <summary>
/// Configuration options for SQL Server connections
/// </summary>
public sealed class SqlOptions
{
    public const string SectionName = "Sql";

    /// <summary>
    /// SQL Server connection string
    /// Should be stored in user-secrets or environment variables in production
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Whether to block DDL operations (DROP, TRUNCATE, ALTER, etc.)
    /// </summary>
    public bool BlockDdlOperations { get; set; } = true;

    /// <summary>
    /// Maximum number of rows to return from queries (0 = unlimited)
    /// </summary>
    public int MaxRowsReturned { get; set; } = 10000;

    /// <summary>
    /// Query timeout in seconds
    /// </summary>
    public int QueryTimeoutSeconds { get; set; } = 30;
}
