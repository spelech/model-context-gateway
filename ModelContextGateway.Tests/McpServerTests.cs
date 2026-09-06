namespace ModelContextGateway.Tests;

public class McpServerTests
{
    [Fact]
    [Requirement("AUTH-05", "AUTH", RequirementType.Positive, "McpServer supports AllowPassThroughAuth flag")]
    public void McpServer_Should_Have_AllowPassThroughAuth()
    {
        var server = new McpServer();
        Assert.False(server.AllowPassThroughAuth); // Default
        server.AllowPassThroughAuth = true;
        Assert.True(server.AllowPassThroughAuth);
    }

    [Fact]
    [Requirement("MCP-27", "MCP", RequirementType.Positive, "McpServer supports Alias property")]
    public void McpServer_Supports_Alias_Property()
    {
        var server = new McpServer
        {
            Id = "postgres-mcp-homebox",
            Alias = "homebox_db",
            DisplayName = "Homebox DB",
            Url = "http://postgres-mcp-homebox:8000/sse"
        };

        Assert.Equal("homebox_db", server.Alias);
    }

    [Fact]
    [Requirement("MCP-27", "MCP", RequirementType.Positive, "DatabaseInitializer.EnsureAliasColumn migrates Servers table with Alias column")]
    public void DatabaseInitializer_EnsureAliasColumn_AddsColumnSuccessfully()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        Dapper.SqlMapper.Execute(conn, "CREATE TABLE Servers (Id TEXT PRIMARY KEY, DisplayName TEXT);");

        ModelContextGateway.Infrastructure.Persistence.DatabaseInitializer.EnsureAliasColumn(conn);

        var cols = Dapper.SqlMapper.Query<string>(conn, "SELECT name FROM pragma_table_info('Servers');").ToList();
        Assert.Contains("Alias", cols);

        // Verify idempotent call does not throw
        ModelContextGateway.Infrastructure.Persistence.DatabaseInitializer.EnsureAliasColumn(conn);
    }
}
