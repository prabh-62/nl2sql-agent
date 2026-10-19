using Azure.Identity;
using Nl2Sql.Agent;
using Nl2Sql.Evals;

// Usage: dotnet run --project tests/Nl2Sql.Evals -- [--models a,b] [--modes discovery,prompt] [--cases id1,id2] [--check-references]
// Locally you sign in with az login (as an Entra admin), so queries switch to the reader user, as in the
// safety tests. In CI, NL2SQL_EVALS_MODE=direct and the job signs in as the nl2sql-reader identity.

// Only developer credentials (Azure CLI, including the one azure/login sets up in CI).
if (Environment.GetEnvironmentVariable("AZURE_TOKEN_CREDENTIALS") is null)
{
    Environment.SetEnvironmentVariable("AZURE_TOKEN_CREDENTIALS", "dev");
}

var arguments = ParseArguments(args);
var models = arguments.GetValueOrDefault("models", "gpt-5.4-mini,gpt-6-luna").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var modes = arguments.GetValueOrDefault("modes", "discovery,prompt").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(m => Enum.Parse<CatalogMode>(m, ignoreCase: true)).ToArray();
var caseFilter = arguments.TryGetValue("cases", out var ids) ? ids.Split(',', StringSplitOptions.TrimEntries).ToHashSet() : null;
var outputDirectory = arguments.GetValueOrDefault("output", Path.Combine("artifacts", "evals"));

var settings = new EvalSettings(
    DatabaseConnectionString: Environment.GetEnvironmentVariable("NL2SQL_EVALS_DB")
        ?? "Server=tcp:panjab.database.windows.net,1433;Database=rasoolpur;Authentication=Active Directory Default;Encrypt=True;Connect Timeout=90",
    FoundryConnectionString: Environment.GetEnvironmentVariable("NL2SQL_EVALS_FOUNDRY")
        ?? "Endpoint=https://hemingford-9901-resource.cognitiveservices.azure.com/;Deployment=gpt-5.4-mini",
    ImpersonateUser: Environment.GetEnvironmentVariable("NL2SQL_EVALS_MODE") == "direct" ? null : "nl2sql_reader_dev",
    Credential: new DefaultAzureCredential(),
    Parallelism: int.Parse(arguments.GetValueOrDefault("parallel", "3"), System.Globalization.CultureInfo.InvariantCulture),
    CaseTimeout: TimeSpan.FromMinutes(3));

var cases = EvalCase.Load(Path.Combine(AppContext.BaseDirectory, "cases.json"))
    .Where(c => caseFilter is null || caseFilter.Contains(c.Id))
    .ToList();
var runner = new EvalRunner(settings);
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };

if (arguments.ContainsKey("check-references"))
{
    // Runs every reference query, so a golden case can't silently expect nothing.
    var bad = 0;
    foreach (var c in cases.Where(c => c.Kind == CaseKind.Answer))
    {
        var rows = await runner.RunReferenceAsync(c, Personas.Find(c.Persona)!, cancel.Token);
        var normalized = ResultComparer_Normalize(rows);
        Console.WriteLine($"{c.Id,-36} {normalized.Count,4} row(s)  {string.Join(" | ", normalized.FirstOrDefault()?.Select(v => v?.ToString()) ?? [])}");
        bad += normalized.Count == 0 ? 1 : 0;
    }

    return bad == 0 ? 0 : 1;
}

var results = new List<CaseResult>();
foreach (var model in models)
{
    foreach (var mode in modes)
    {
        Console.WriteLine($"Running {cases.Count} cases with {model}, catalog {mode}…");
        results.AddRange(await runner.RunAsync(model, mode, cases, cancel.Token));
    }
}

var markdown = Report.Markdown(results, runner.Defaults.MaxIterations);
Console.WriteLine();
Console.WriteLine(markdown);

Directory.CreateDirectory(outputDirectory);
await File.WriteAllTextAsync(Path.Combine(outputDirectory, "results.md"), markdown);
await File.WriteAllTextAsync(Path.Combine(outputDirectory, "results.json"), Report.Json(results));
if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summary)
{
    await File.AppendAllTextAsync(summary, markdown);
}

// Isolation and refusal cases must always pass; accuracy is reported, not enforced.
var hardFailures = results.Where(r => r.Case.IsHard && r.Outcome != Outcome.Pass).ToList();
foreach (var failure in hardFailures)
{
    Console.Error.WriteLine($"MUST-PASS CASE FAILED: {failure.Case.Id} ({failure.Model}, {failure.Mode}): {failure.Outcome} {failure.Detail}");
}

return hardFailures.Count == 0 ? 0 : 1;

static Dictionary<string, string> ParseArguments(string[] args)
{
    var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = args[i][2..];
        var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
        parsed[key] = hasValue ? args[++i] : "true";
    }

    return parsed;
}

static List<object?[]> ResultComparer_Normalize(List<object?[]> rows) => ResultComparer.Normalize(rows);
