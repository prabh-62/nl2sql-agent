using Nl2Sql.Data;

namespace Nl2Sql.UnitTests;

public sealed class SqlGuardTests
{
    public static TheoryData<string> AllowedQueries =>
    [
        "SELECT TOP (10) OrderId, NetRevenue FROM ai.Orders ORDER BY NetRevenue DESC",
        "select orderid from AI.ORDERS",
        "SELECT o.LocationName, SUM(o.NetRevenue) AS Revenue FROM [ai].[Orders] AS o GROUP BY o.LocationName",
        "SELECT LocationId, LocalHour, COUNT(*) AS Orders FROM ai.Orders WHERE LocalDate >= DATEADD(day, -7, CAST(GETDATE() AS date)) GROUP BY LocationId, LocalHour",
        "SELECT l.MenuItemName, SUM(l.LineNetRevenue) FROM ai.OrderLines l JOIN ai.MenuItems m ON m.MenuItemId = l.MenuItemId GROUP BY l.MenuItemName",
        "WITH monthly AS (SELECT DATEFROMPARTS(YEAR(LocalDate), MONTH(LocalDate), 1) AS Month, MilkType FROM ai.OrderLines WHERE IsLatte = 1) SELECT Month, AVG(CASE WHEN MilkType = 'oat' THEN 1.0 ELSE 0 END) FROM monthly GROUP BY Month",
        "SELECT CustomerId FROM ai.Customers c WHERE NOT EXISTS (SELECT 1 FROM ai.Orders o WHERE o.CustomerId = c.CustomerId AND o.LocalDate > DATEADD(day, -30, CURRENT_TIMESTAMP))",
        "SELECT Channel FROM ai.Orders UNION ALL SELECT Channel FROM ai.Refunds",
        "SELECT * FROM (SELECT Channel, NetRevenue FROM ai.Orders) AS src PIVOT (SUM(NetRevenue) FOR Channel IN ([mobile], [in_store], [delivery])) AS p",
        "SELECT v.n FROM (VALUES (1), (2)) AS v(n)",
        "SELECT 1",
        "SELECT TOP (5) Name FROM ai.MenuItems; ",
        "-- busiest hour\nSELECT LocalHour, COUNT(*) FROM ai.Orders /* last week */ GROUP BY LocalHour",
        "SELECT LocationName, ROW_NUMBER() OVER (PARTITION BY LocationId ORDER BY NetRevenue DESC) FROM ai.Orders",
        "SELECT Question FROM ai.Examples",
        "SELECT LocationName FROM ai.Orders FOR JSON PATH",
    ];

    public static TheoryData<string, string> RejectedQueries => new()
    {
        // Not a single SELECT.
        { "", "empty" },
        { "   ", "empty" },
        { "DELETE FROM ai.Refunds", "Only SELECT" },
        { "UPDATE ai.Orders SET Channel = 'mobile'", "Only SELECT" },
        { "INSERT INTO ai.Examples (Question) VALUES ('x')", "Only SELECT" },
        { "DROP TABLE dbo.Orders", "Only SELECT" },
        { "TRUNCATE TABLE dbo.Refunds", "Only SELECT" },
        { "MERGE ai.Orders AS t USING ai.Orders AS s ON t.OrderId = s.OrderId WHEN MATCHED THEN DELETE;", "Only SELECT" },
        { "EXEC sp_executesql N'SELECT 1'", "Only SELECT" },
        { "EXECUTE ('DELETE FROM dbo.Refunds')", "Only SELECT" },
        { "WAITFOR DELAY '00:00:10'", "Only SELECT" },
        { "DECLARE @x int = 1; SELECT @x", "exactly one" },
        { "SELECT 1; SELECT 2", "exactly one" },
        { "SELECT 1; DELETE FROM dbo.Refunds", "exactly one" },
        { "SELECT 1\nGO\nSELECT 2", "exactly one" },
        { "SELECT 1 -- ok\n; DROP TABLE dbo.Orders", "exactly one" },
        { "SELECT * INTO ai.Copy FROM ai.Orders", "INTO" },
        { "SELECT * INTO #temp FROM ai.Orders", "INTO" },
        { "SET CONTEXT_INFO 0x01", "Only SELECT" },
        { "REVERT", "Only SELECT" },
        { "EXECUTE AS USER = 'dbo'", "Only SELECT" },
        // Outside schema ai.
        { "SELECT * FROM dbo.Orders", "outside schema" },
        { "SELECT * FROM Orders", "needs its schema" },
        { "SELECT * FROM [dbo].[Orders]", "outside schema" },
        { "SELECT * FROM sys.objects", "outside schema" },
        { "SELECT * FROM INFORMATION_SCHEMA.TABLES", "outside schema" },
        { "SELECT * FROM security.TenantPolicy", "outside schema" },
        { "SELECT * FROM ai.Orders o JOIN dbo.Customers c ON c.CustomerId = o.CustomerId", "outside schema" },
        { "SELECT * FROM ai.Orders WHERE OrderId IN (SELECT OrderId FROM dbo.Refunds)", "outside schema" },
        { "WITH x AS (SELECT * FROM dbo.Orders) SELECT * FROM x", "outside schema" },
        { "SELECT * FROM rasoolpur.ai.Orders", "another database" },
        { "SELECT * FROM master.dbo.sysdatabases", "another database" },
        { "SELECT * FROM otherserver.rasoolpur.ai.Orders", "another database" },
        { "SELECT * FROM rasoolpur..Orders", "another database" },
        // Rowset functions and other table sources.
        { "SELECT * FROM OPENROWSET('SQLNCLI', 'Server=evil;', 'SELECT 1')", "OPENROWSET" },
        { "SELECT * FROM OPENROWSET(BULK '/etc/passwd', SINGLE_CLOB) AS x", "OPENROWSET" },
        { "SELECT * FROM OPENQUERY(evil, 'SELECT 1')", "OPENQUERY" },
        { "SELECT * FROM OPENDATASOURCE('SQLNCLI', 'Data Source=evil').db.dbo.t", "OPENDATASOURCE" },
        { "SELECT * FROM OPENJSON(N'[1]')", "OPENJSON" },
        { "SELECT * FROM sys.dm_exec_sessions", "outside schema" },
        { "SELECT * FROM sys.fn_my_permissions(NULL, 'DATABASE')", "not allowed" },
        { "SELECT * FROM dbo.fn_whatever()", "not allowed" },
        { "SELECT value FROM STRING_SPLIT('a,b', ',')", "not allowed" },
        // Functions, variables and hints.
        { "SELECT dbo.fn_whatever(1)", "only built-in functions" },
        { "SELECT ai.Something(1)", "only built-in functions" },
        { "SELECT OBJECT_DEFINITION(OBJECT_ID('ai.Orders'))", "OBJECT_DEFINITION" },
        { "SELECT SESSION_CONTEXT(N'BrandId')", "SESSION_CONTEXT" },
        { "SELECT USER_NAME()", "USER_NAME" },
        { "SELECT is_rolemember('db_owner')", "IS_ROLEMEMBER" },
        { "SELECT HAS_PERMS_BY_NAME('dbo.Orders', 'OBJECT', 'SELECT')", "HAS_PERMS_BY_NAME" },
        { "SELECT CURRENT_USER", "not allowed" },
        { "SELECT SYSTEM_USER", "not allowed" },
        { "SELECT @@VERSION", "System variables" },
        { "SELECT @@SERVERNAME", "System variables" },
        { "SELECT @x", "Variables" },
        { "SELECT NEXT VALUE FOR dbo.OrderNumbers", "NEXT VALUE FOR" },
        { "SELECT * FROM ai.Orders WITH (NOLOCK)", "Table hints" },
        { "SELECT * FROM ai.Orders OPTION (MAXDOP 1)", "Query hints" },
        { "SELECT * FROM ai.Orders OPTION (MAXRECURSION 0)", "Query hints" },
        // Doesn't parse.
        { "SELEC * FROM ai.Orders", "Syntax error" },
        { "SELECT * FROM ai.Orders WHERE", "Syntax error" },
        { "SELECT 'unterminated FROM ai.Orders", "Syntax error" },
    };

    [Theory]
    [MemberData(nameof(AllowedQueries))]
    public void Allows(string sql)
    {
        var result = SqlGuard.Check(sql);

        Assert.True(result.IsAllowed, string.Join(" | ", result.Errors));
        Assert.Empty(result.Errors);
    }

    [Theory]
    [MemberData(nameof(RejectedQueries))]
    public void Rejects(string sql, string expectedError)
    {
        var result = SqlGuard.Check(sql);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Errors, e => e.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Lists_the_views_a_query_reads()
    {
        var result = SqlGuard.Check("WITH c AS (SELECT CustomerId FROM ai.Customers) SELECT * FROM ai.Orders o JOIN c ON c.CustomerId = o.CustomerId JOIN AI.orders o2 ON o2.OrderId = o.OrderId");

        Assert.Equal(["ai.Customers", "ai.Orders"], result.Views);
    }

    [Fact]
    public void Rejects_very_long_queries() =>
        Assert.False(SqlGuard.Check("SELECT 1" + new string(' ', SqlGuard.MaxLength)).IsAllowed);

    [Fact]
    public void Error_messages_tell_the_model_how_to_fix_the_query()
    {
        var result = SqlGuard.Check("SELECT * FROM Orders");

        Assert.Equal(["'Orders' needs its schema: write ai.Orders."], result.Errors);
    }
}
