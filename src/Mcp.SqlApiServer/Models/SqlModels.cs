using System.Text.Json.Serialization;

namespace Mcp.SqlApiServer.Models;

/// <summary>
/// Parameters for sql.query tool
/// </summary>
public sealed class SqlQueryParams
{
    [JsonPropertyName("sql")]
    public string Sql { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?>? Parameters { get; set; }
}

/// <summary>
/// Result from sql.query tool
/// </summary>
public sealed class SqlQueryResult
{
    [JsonPropertyName("rows")]
    public List<Dictionary<string, object?>> Rows { get; set; } = new();

    [JsonPropertyName("rowCount")]
    public int RowCount => Rows.Count;
}

/// <summary>
/// Parameters for sql.execute tool
/// </summary>
public sealed class SqlExecuteParams
{
    [JsonPropertyName("sql")]
    public string Sql { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?>? Parameters { get; set; }
}

/// <summary>
/// Result from sql.execute tool
/// </summary>
public sealed class SqlExecuteResult
{
    [JsonPropertyName("rowsAffected")]
    public int RowsAffected { get; set; }
}
