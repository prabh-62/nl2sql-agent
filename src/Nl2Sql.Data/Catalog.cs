using Microsoft.Data.SqlClient;

namespace Nl2Sql.Data;

public sealed record ViewInfo(string Name, string? Description);

public sealed record ColumnInfo(string Name, string Type, bool IsNullable, string? Description);

public sealed record Example(string Question, string Sql, string? Notes);

/// <summary>
/// The semantic layer as the model sees it: the views in schema <c>ai</c> and their descriptions,
/// read live from the database (MS_Description extended properties), plus worked examples.
/// Everything is read through a <see cref="ReaderSession"/>, so it's exactly what the reader can see.
/// </summary>
public static class Catalog
{
    public static async Task<IReadOnlyList<ViewInfo>> ListViewsAsync(ReaderSession session, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.name, CAST(ep.value AS nvarchar(4000))
            FROM sys.views AS v
            LEFT JOIN sys.extended_properties AS ep
                ON ep.class = 1 AND ep.major_id = v.object_id AND ep.minor_id = 0 AND ep.name = N'MS_Description'
            WHERE v.schema_id = SCHEMA_ID(N'ai')
            ORDER BY v.name;
            """;

        var views = new List<ViewInfo>();
        await using var command = new SqlCommand(sql, session.Connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            views.Add(new ViewInfo($"ai.{reader.GetString(0)}", reader.IsDBNull(1) ? null : reader.GetString(1)));
        }

        return views;
    }

    /// <summary>Columns of one ai view, or null if there's no such view.</summary>
    public static async Task<IReadOnlyList<ColumnInfo>?> DescribeViewAsync(ReaderSession session, string viewName, CancellationToken cancellationToken)
    {
        // Accept "Orders", "ai.Orders" or "[ai].[Orders]"; the name is only ever a parameter.
        var name = viewName.Replace("[", "", StringComparison.Ordinal).Replace("]", "", StringComparison.Ordinal);
        if (name.StartsWith("ai.", StringComparison.OrdinalIgnoreCase))
        {
            name = name[3..];
        }

        const string sql = """
            SELECT c.name,
                   TYPE_NAME(c.user_type_id)
                       + CASE WHEN TYPE_NAME(c.user_type_id) IN (N'decimal', N'numeric') THEN CONCAT(N'(', c.precision, N',', c.scale, N')')
                              WHEN TYPE_NAME(c.user_type_id) IN (N'varchar', N'char') THEN CONCAT(N'(', c.max_length, N')')
                              WHEN TYPE_NAME(c.user_type_id) IN (N'nvarchar', N'nchar') THEN CONCAT(N'(', c.max_length / 2, N')')
                              ELSE N'' END,
                   c.is_nullable,
                   CAST(ep.value AS nvarchar(4000))
            FROM sys.views AS v
            JOIN sys.columns AS c ON c.object_id = v.object_id
            LEFT JOIN sys.extended_properties AS ep
                ON ep.class = 1 AND ep.major_id = v.object_id AND ep.minor_id = c.column_id AND ep.name = N'MS_Description'
            WHERE v.schema_id = SCHEMA_ID(N'ai') AND v.name = @name
            ORDER BY c.column_id;
            """;

        var columns = new List<ColumnInfo>();
        await using var command = new SqlCommand(sql, session.Connection);
        command.Parameters.AddWithValue("@name", name);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new ColumnInfo(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return columns.Count > 0 ? columns : null;
    }

    /// <summary>The examples whose questions share the most words with <paramref name="question"/>.</summary>
    public static async Task<IReadOnlyList<Example>> GetExamplesAsync(ReaderSession session, string question, int count, CancellationToken cancellationToken)
    {
        var examples = new List<Example>();
        await using (var command = new SqlCommand("SELECT Question, [Sql], Notes FROM ai.Examples;", session.Connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                examples.Add(new Example(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
        }

        // Plain word overlap is enough for a handful of examples; embeddings can replace it later.
        var words = Words(question);
        return examples
            .Select(e => (Example: e, Score: Words(e.Question).Count(words.Contains)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Example.Question, StringComparer.Ordinal)
            .Take(count)
            .Select(x => x.Example)
            .ToArray();
    }

    private static HashSet<string> Words(string text) =>
        text.Split([' ', ',', '.', '?', '!', '-', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length > 2)
            .ToHashSet();
}
