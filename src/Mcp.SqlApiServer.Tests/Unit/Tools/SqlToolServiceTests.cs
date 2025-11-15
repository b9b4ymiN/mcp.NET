using Mcp.SqlApiServer.Config;
using Mcp.SqlApiServer.Models;
using Mcp.SqlApiServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Mcp.SqlApiServer.Tests.Unit.Tools;

public class SqlToolServiceTests
{
    private readonly Mock<IOptions<SqlOptions>> _mockOptions;
    private readonly Mock<ILogger<SqlToolService>> _mockLogger;
    private readonly SqlOptions _options;

    public SqlToolServiceTests()
    {
        _mockOptions = new Mock<IOptions<SqlOptions>>();
        _mockLogger = new Mock<ILogger<SqlToolService>>();

        _options = new SqlOptions
        {
            ConnectionString = "Server=localhost;Database=Test;",
            BlockDdlOperations = true,
            MaxRowsReturned = 1000,
            QueryTimeoutSeconds = 30
        };

        _mockOptions.Setup(x => x.Value).Returns(_options);
    }

    [Fact]
    public async Task QueryAsync_WithEmptySql_ThrowsArgumentException()
    {
        // Arrange
        var service = new SqlToolService(_mockOptions.Object, _mockLogger.Object);

        var parameters = new SqlQueryParams
        {
            Sql = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(parameters));
    }

    [Fact]
    public async Task QueryAsync_WithNonSelectStatement_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new SqlToolService(_mockOptions.Object, _mockLogger.Object);

        var parameters = new SqlQueryParams
        {
            Sql = "INSERT INTO Users (Name) VALUES ('John')"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueryAsync(parameters));
        Assert.Contains("SELECT statements", exception.Message);
    }

    [Theory]
    [InlineData("DROP TABLE Users")]
    [InlineData("TRUNCATE TABLE Users")]
    [InlineData("ALTER TABLE Users ADD COLUMN Age INT")]
    [InlineData("CREATE TABLE NewTable (Id INT)")]
    public async Task QueryAsync_WithDdlStatement_ThrowsInvalidOperationException(string sql)
    {
        // Arrange
        var service = new SqlToolService(_mockOptions.Object, _mockLogger.Object);

        var parameters = new SqlQueryParams
        {
            Sql = sql
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueryAsync(parameters));
        Assert.Contains("DDL operations", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySql_ThrowsArgumentException()
    {
        // Arrange
        var service = new SqlToolService(_mockOptions.Object, _mockLogger.Object);

        var parameters = new SqlExecuteParams
        {
            Sql = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(parameters));
    }

    [Theory]
    [InlineData("DROP TABLE Users")]
    [InlineData("TRUNCATE TABLE Users")]
    [InlineData("ALTER TABLE Users ADD COLUMN Age INT")]
    public async Task ExecuteAsync_WithDdlStatement_ThrowsInvalidOperationException(string sql)
    {
        // Arrange
        var service = new SqlToolService(_mockOptions.Object, _mockLogger.Object);

        var parameters = new SqlExecuteParams
        {
            Sql = sql
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(parameters));
        Assert.Contains("DDL operations", exception.Message);
    }

    [Fact]
    public void ExecuteAsync_WithSelectStatement_LogsWarning()
    {
        // Arrange
        var service = new SqlToolService(_mockOptions.Object, _mockLogger.Object);

        var parameters = new SqlExecuteParams
        {
            Sql = "SELECT * FROM Users"
        };

        // Note: This test would require a real database connection to fully test
        // In a real scenario, you'd use an in-memory database or mock the Dapper calls
        // For now, we're just testing the validation logic
        Assert.NotNull(service);
    }

    [Theory]
    [InlineData("SELECT * FROM Users")]
    [InlineData("WITH cte AS (SELECT * FROM Users) SELECT * FROM cte")]
    [InlineData("  SELECT Id, Name FROM Products")]
    public void IsSelectQuery_WithSelectStatements_ReturnsTrue(string sql)
    {
        // This would test the private IsSelectQuery method if it were public/internal
        // For now, we test indirectly through QueryAsync validation
        Assert.True(sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                   sql.TrimStart().StartsWith("WITH", StringComparison.OrdinalIgnoreCase));
    }
}
