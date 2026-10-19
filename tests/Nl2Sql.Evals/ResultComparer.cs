using System.Globalization;

namespace Nl2Sql.Evals;

/// <summary>
/// Decides whether the agent's result answers the question, by comparing it with the reference
/// result: values, not SQL text and not column names.
/// <list type="bullet">
/// <item>Each reference column must match some column of the agent's result; extra agent columns are fine.</item>
/// <item>Rows are compared as a multiset unless the case is ordered.</item>
/// <item>Numbers match within 0.5% (or 0.01), and a share may be given as a fraction or a percentage.</item>
/// <item>Rows that are entirely null (an aggregate over nothing) count as no rows.</item>
/// </list>
/// </summary>
internal static class ResultComparer
{
    private static readonly double[] ScaleFactors = [1, 100, 0.01];

    public static bool Matches(
        IReadOnlyList<object?[]> reference,
        IReadOnlyList<object?[]> actual,
        bool ordered = false,
        bool prefix = false)
    {
        var expected = Normalize(reference);
        var got = Normalize(actual);

        if (expected.Count == 0)
        {
            return got.Count == 0;
        }

        if (prefix && ordered)
        {
            if (got.Count < expected.Count)
            {
                return false;
            }

            got = got.Take(expected.Count).ToList();
        }
        else if (got.Count != expected.Count)
        {
            return false;
        }

        var columns = expected[0].Length;
        return MapColumns(expected, got, ordered, column: 0, new int[columns], new double[columns], new bool[got[0].Length]);
    }

    // Backtracking: assign each reference column to an unused agent column (and a scale factor),
    // then check the whole rows, not just each column on its own.
    private static bool MapColumns(List<object?[]> expected, List<object?[]> got, bool ordered, int column, int[] map, double[] scales, bool[] used)
    {
        if (column == map.Length)
        {
            return RowsMatch(expected, got, ordered, map, scales);
        }

        for (var candidate = 0; candidate < used.Length; candidate++)
        {
            if (used[candidate])
            {
                continue;
            }

            foreach (var scale in ScaleFactors)
            {
                if (!ColumnMatches(expected, got, column, candidate, scale, ordered))
                {
                    continue;
                }

                used[candidate] = true;
                map[column] = candidate;
                scales[column] = scale;
                if (MapColumns(expected, got, ordered, column + 1, map, scales, used))
                {
                    return true;
                }

                used[candidate] = false;
            }
        }

        return false;
    }

    private static bool ColumnMatches(List<object?[]> expected, List<object?[]> got, int column, int candidate, double scale, bool ordered)
    {
        var e = expected.Select(r => r[column]).ToList();
        var g = got.Select(r => r[candidate]).ToList();
        if (!ordered)
        {
            e = e.Order(CellComparer.Instance).ToList();
            g = g.Order(CellComparer.Instance).ToList();
        }

        return e.Zip(g).All(pair => CellEquals(pair.First, pair.Second, scale));
    }

    private static bool RowsMatch(List<object?[]> expected, List<object?[]> got, bool ordered, int[] map, double[] scales)
    {
        var projected = got.Select(r => map.Select(i => r[i]).ToArray()).ToList();
        IEnumerable<object?[]> e = expected, g = projected;
        if (!ordered)
        {
            e = expected.Order(RowComparer.Instance);
            g = projected.Order(RowComparer.Instance);
        }

        return e.Zip(g).All(pair => pair.First.Zip(pair.Second).Select((cells, i) => CellEquals(cells.First, cells.Second, scales[i])).All(ok => ok));
    }

    private static bool CellEquals(object? expected, object? actual, double scale) => (expected, actual) switch
    {
        (null, null) => true,
        (double e, double a) => Math.Abs(a / scale - e) <= Math.Max(0.01, 0.005 * Math.Abs(e)),
        (string e, string a) => string.Equals(e, a, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    /// <summary>Numbers become doubles, dates become ISO text, strings are trimmed; all-null rows are dropped.</summary>
    internal static List<object?[]> Normalize(IReadOnlyList<object?[]> rows) =>
        rows.Select(row => row.Select(NormalizeCell).ToArray())
            .Where(row => row.Any(cell => cell is not null))
            .ToList();

    private static object? NormalizeCell(object? value) => value switch
    {
        null or DBNull => null,
        bool b => b ? 1d : 0d,
        byte or short or int or long or float or double or decimal => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        DateTime d when d.TimeOfDay == TimeSpan.Zero => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("s", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.UtcDateTime.ToString("s", CultureInfo.InvariantCulture),
        string s => s.Trim(),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim(),
    };

    private sealed class CellComparer : IComparer<object?>
    {
        public static readonly CellComparer Instance = new();

        public int Compare(object? x, object? y) => (x, y) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            (double a, double b) => a.CompareTo(b),
            (string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase),
            (double, _) => -1,
            _ => 1,
        };
    }

    private sealed class RowComparer : IComparer<object?[]>
    {
        public static readonly RowComparer Instance = new();

        public int Compare(object?[]? x, object?[]? y)
        {
            for (var i = 0; i < Math.Min(x!.Length, y!.Length); i++)
            {
                var result = CellComparer.Instance.Compare(x[i], y[i]);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }
    }
}
