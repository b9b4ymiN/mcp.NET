using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Mcp.SqlApiServer.Config;
using Mcp.SqlApiServer.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mcp.SqlApiServer.Tools;

/// <summary>
/// Service for executing SQL Server operations
/// </summary>
public sealed class SqlToolService
{
    private readonly SqlOptions _options;
    private readonly ILogger<SqlToolService> _logger;

    // Dangerous SQL keywords that indicate non-SELECT or DDL operations
    private static readonly string[] DdlKeywords = new[]
    {
        "DROP", "TRUNCATE", "ALTER", "CREATE", "EXEC", "EXECUTE", "SP_"
    };

    private static readonly string[] WriteKeywords = new[]
    {
        "INSERT", "UPDATE", "DELETE", "MERGE"
    };

    public SqlToolService(
        IOptions<SqlOptions> options,
        ILogger<SqlToolService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Execute a SELECT query and return rows
    /// </summary>
    public async Task<SqlQueryResult> QueryAsync(
        SqlQueryParams parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parameters.Sql))
        {
            throw new ArgumentException("SQL query is required", nameof(parameters));
        }

        // Safety check: ensure this is a SELECT query
        if (!IsSelectQuery(parameters.Sql))
        {
            throw new InvalidOperationException(
                "sql.query only accepts SELECT statements. Use sql.execute for INSERT/UPDATE/DELETE.");
        }

        // Block DDL operations if configured
        if (_options.BlockDdlOperations && ContainsDdlKeywords(parameters.Sql))
        {
            throw new InvalidOperationException(
                "DDL operations (DROP, TRUNCATE, ALTER, CREATE, etc.) are not allowed.");
        }

        _logger.LogInformation("Executing SQL query: {Sql}", SanitizeSqlForLogging(parameters.Sql));

        try
        {
            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Build DynamicParameters
            var dynamicParams = BuildDynamicParameters(parameters.Parameters);

            // Execute query
            var rows = await connection.QueryAsync(
                parameters.Sql,
                dynamicParams,
                commandTimeout: _options.QueryTimeoutSeconds);

            // Convert to list of dictionaries
            var rowList = rows
                .Select(row => (IDictionary<string, object>)row)
                .Select(dict => dict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value))
                .ToList();

            // Apply max rows limit
            if (_options.MaxRowsReturned > 0 && rowList.Count > _options.MaxRowsReturned)
            {
                _logger.LogWarning(
                    "Query returned {Count} rows, limiting to {Max}",
                    rowList.Count,
                    _options.MaxRowsReturned);

                rowList = rowList.Take(_options.MaxRowsReturned).ToList();
            }

            _logger.LogInformation("Query returned {Count} rows", rowList.Count);

            return new SqlQueryResult { Rows = rowList };
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL query failed: {Message}", ex.Message);
            throw new InvalidOperationException($"SQL query failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Execute an INSERT/UPDATE/DELETE statement and return rows affected
    /// </summary>
    public async Task<SqlExecuteResult> ExecuteAsync(
        SqlExecuteParams parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parameters.Sql))
        {
            throw new ArgumentException("SQL statement is required", nameof(parameters));
        }

        // Block DDL operations if configured
        if (_options.BlockDdlOperations && ContainsDdlKeywords(parameters.Sql))
        {
            throw new InvalidOperationException(
                "DDL operations (DROP, TRUNCATE, ALTER, CREATE, etc.) are not allowed.");
        }

        // Warn if this looks like a SELECT query
        if (IsSelectQuery(parameters.Sql))
        {
            _logger.LogWarning("sql.execute called with SELECT query. Consider using sql.query instead.");
        }

        _logger.LogInformation("Executing SQL statement: {Sql}", SanitizeSqlForLogging(parameters.Sql));

        try
        {
            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Build DynamicParameters
            var dynamicParams = BuildDynamicParameters(parameters.Parameters);

            // Execute statement
            var rowsAffected = await connection.ExecuteAsync(
                parameters.Sql,
                dynamicParams,
                commandTimeout: _options.QueryTimeoutSeconds);

            _logger.LogInformation("SQL statement affected {Count} rows", rowsAffected);

            return new SqlExecuteResult { RowsAffected = rowsAffected };
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL execution failed: {Message}", ex.Message);
            throw new InvalidOperationException($"SQL execution failed: {ex.Message}", ex);
        }
    }

    private static bool IsSelectQuery(string sql)
    {
        // Trim and get first meaningful keyword
        var trimmed = sql.TrimStart();
        return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase); // Common Table Expressions
    }

    private static bool ContainsDdlKeywords(string sql)
    {
        var upperSql = sql.ToUpperInvariant();
        return DdlKeywords.Any(keyword => Regex.IsMatch(upperSql, $@"\b{keyword}\b"));
    }

    private static DynamicParameters BuildDynamicParameters(Dictionary<string, object?>? parameters)
    {
        var dynamicParams = new DynamicParameters();

        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                dynamicParams.Add(key, value);
            }
        }

        return dynamicParams;
    }

    private static string SanitizeSqlForLogging(string sql)
    {
        // Truncate long queries for logging
        const int maxLength = 200;
        if (sql.Length <= maxLength)
        {
            return sql;
        }

        return sql.Substring(0, maxLength) + "...";
    }
}
