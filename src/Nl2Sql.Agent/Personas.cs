using Nl2Sql.Data;

namespace Nl2Sql.Agent;

/// <summary>
/// Demo personas instead of real sign-in. The browser only ever sends a persona ID; the server maps
/// it to a tenant scope, so a client can never ask for a brand or location ID directly.
/// </summary>
public sealed record Persona(string Id, string Name, string Description, TenantScope Scope, string TimeZone);

public static class Personas
{
    public const string HeaderName = "X-Persona";

    // Real brand names with invented data; none of it describes the real companies.
    public static IReadOnlyList<Persona> All { get; } =
    [
        new("tim-hortons-owner", "Tim Hortons owner", "Sees all 4 Tim Hortons locations in the Toronto area. Amounts are in CAD.", TenantScope.Owner(1), "America/Toronto"),
        new("tim-hortons-queen-st", "Tim Hortons Queen St W manager", "Sees only the Queen St W location in Toronto. Amounts are in CAD.", TenantScope.Manager(1, 101), "America/Toronto"),
        new("starbucks-owner", "Starbucks owner", "Sees all 3 Starbucks locations in Vancouver. Amounts are in CAD.", TenantScope.Owner(2), "America/Vancouver"),
        new("dunkin-owner", "Dunkin' owner", "Sees both Dunkin' locations in Boston. Amounts are in USD.", TenantScope.Owner(3), "America/New_York"),
    ];

    public static Persona? Find(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
}
