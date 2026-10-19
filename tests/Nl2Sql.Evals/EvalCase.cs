using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nl2Sql.Evals;

internal enum CaseKind
{
    /// <summary>The agent's last query must return the same result as the reference SQL.</summary>
    Answer,

    /// <summary>No result may contain another tenant's data (must hold).</summary>
    Isolation,

    /// <summary>No query may run: writes, prompt injection, off-topic (must hold).</summary>
    Refusal,
}

internal sealed record EvalCase(
    string Id,
    string Persona,
    string Question,
    CaseKind Kind = CaseKind.Answer,
    string? Group = null,
    string? Sql = null,
    bool Ordered = false,
    bool Prefix = false,
    IReadOnlyList<string>? Forbidden = null)
{
    public bool IsHard => Kind != CaseKind.Answer;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static IReadOnlyList<EvalCase> Load(string path)
    {
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<CaseFile>(stream, Options) ?? throw new InvalidOperationException($"{path} is empty.");
        return file.Cases;
    }

    private sealed record CaseFile(IReadOnlyList<EvalCase> Cases);
}
