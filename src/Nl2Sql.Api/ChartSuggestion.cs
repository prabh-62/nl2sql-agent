using System.ComponentModel;

namespace Nl2Sql.Api;

/// <summary>
/// A chart the model suggests for a query's result. The UI draws it with Recharts after validating it
/// again; the server checks it against the actual result columns first.
/// </summary>
public sealed record ChartSuggestion(
    [property: Description("bar (compare categories) or line (a trend over time).")] string Type,
    [property: Description("Short chart title, for example \"Net revenue by location, September\".")] string Title,
    [property: Description("Result column for the x axis: the categories or dates.")] string X,
    [property: Description("One to three numeric result columns to plot.")] IReadOnlyList<string> Y)
{
    public static readonly IReadOnlySet<string> Types = new HashSet<string>(StringComparer.Ordinal) { "bar", "line" };

    /// <summary>Null when the chart fits the result; otherwise why not, for the model.</summary>
    public string? Validate(IReadOnlyList<string> columns, IReadOnlyList<object?[]> rows)
    {
        if (!Types.Contains(Type))
        {
            return $"Chart type must be bar or line, not '{Type}'.";
        }

        if (Y is not { Count: >= 1 and <= 3 })
        {
            return "A chart needs one to three y columns.";
        }

        var index = columns.Select((name, i) => (name, i)).ToDictionary(c => c.name, c => c.i, StringComparer.OrdinalIgnoreCase);
        foreach (var column in Y.Prepend(X))
        {
            if (!index.ContainsKey(column))
            {
                return $"'{column}' is not a column of the result. Columns: {string.Join(", ", columns)}.";
            }
        }

        if (rows.Count < 2)
        {
            return "A chart needs at least two rows.";
        }

        foreach (var column in Y)
        {
            if (rows.Any(r => r[index[column]] is not (null or byte or short or int or long or decimal or double or float)))
            {
                return $"'{column}' is not numeric, so it can't be plotted.";
            }
        }

        return null;
    }
}
