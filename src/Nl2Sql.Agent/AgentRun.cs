using Nl2Sql.Data;

namespace Nl2Sql.Agent;

/// <summary>
/// Everything that belongs to one question: whose data it may see, and what happened while
/// answering it. The API keeps one per HTTP request; the evals keep one per case.
/// </summary>
public sealed class AgentRun(Persona persona)
{
    public Persona Persona { get; } = persona;

    /// <summary>Queries that failed (guard, compile or SQL error), counted against the retry limit.</summary>
    public int FailedQueries { get; set; }

    /// <summary>Every query that succeeded, in order. The last one is the answer.</summary>
    public List<QueryResult> Queries { get; } = [];

    /// <summary>Calls to the model while answering, including the final answer.</summary>
    public int ModelCalls { get; set; }
}

/// <summary>Finds the <see cref="AgentRun"/> of the question being answered right now.</summary>
public interface IAgentRunAccessor
{
    AgentRun? Current { get; }
}
