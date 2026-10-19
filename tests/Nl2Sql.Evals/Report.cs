using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nl2Sql.Evals;

internal static class Report
{
    public static string Markdown(IReadOnlyList<CaseResult> results, int maxIterations)
    {
        var text = new StringBuilder();
        text.AppendLine("## nl2sql-agent evals").AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"{results.Select(r => r.Case.Id).Distinct().Count()} cases, run {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.").AppendLine();
        text.AppendLine("| Model | Catalog | Accuracy | Unseen | Paraphrase | Must-pass | Median latency | p95 latency | Avg tokens in / out | Avg model calls | Hit call limit |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");

        foreach (var cell in results.GroupBy(r => (r.Model, r.Mode)))
        {
            var answers = cell.Where(r => !r.Case.IsHard).ToList();
            var hard = cell.Where(r => r.Case.IsHard).ToList();
            var latencies = cell.Select(r => r.Latency.TotalSeconds).Order().ToArray();
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {cell.Key.Model} | {cell.Key.Mode} | {Rate(answers)} | {Rate(answers.Where(r => r.Case.Group == "unseen"))} | {Rate(answers.Where(r => r.Case.Group == "paraphrase"))} " +
                $"| {hard.Count(r => r.Outcome == Outcome.Pass)}/{hard.Count} | {Percentile(latencies, 0.5):0.0} s | {Percentile(latencies, 0.95):0.0} s " +
                $"| {cell.Average(r => r.InputTokens):0} / {cell.Average(r => r.OutputTokens):0} | {cell.Average(r => r.ModelCalls):0.0} | {cell.Count(r => r.ModelCalls >= maxIterations)} |");
        }

        var failures = results.Where(r => r.Outcome != Outcome.Pass).OrderBy(r => r.Case.Id).ThenBy(r => r.Model).ToList();
        if (failures.Count > 0)
        {
            text.AppendLine().AppendLine("### Not passed").AppendLine();
            text.AppendLine("| Case | Model | Catalog | Outcome | Detail |").AppendLine("|---|---|---|---|---|");
            foreach (var r in failures)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"| {r.Case.Id} | {r.Model} | {r.Mode} | {r.Outcome} | {Escape(r.Detail)} |");
            }
        }

        return text.ToString();
    }

    public static string Json(IReadOnlyList<CaseResult> results) => JsonSerializer.Serialize(
        results.Select(r => new
        {
            r.Case.Id,
            r.Case.Persona,
            r.Case.Question,
            Kind = r.Case.Kind.ToString(),
            r.Case.Group,
            r.Model,
            Mode = r.Mode.ToString(),
            Outcome = r.Outcome.ToString(),
            LatencySeconds = Math.Round(r.Latency.TotalSeconds, 2),
            r.InputTokens,
            r.OutputTokens,
            r.ModelCalls,
            r.Sql,
            r.Answer,
            r.Detail,
        }),
        JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string Rate(IEnumerable<CaseResult> results)
    {
        var list = results.ToList();
        return list.Count == 0 ? "–" : $"{100.0 * list.Count(r => r.Outcome == Outcome.Pass) / list.Count:0}% ({list.Count(r => r.Outcome == Outcome.Pass)}/{list.Count})";
    }

    private static double Percentile(double[] sorted, double p) =>
        sorted.Length == 0 ? 0 : sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1)];

    private static string Escape(string? value) => (value ?? "").Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
