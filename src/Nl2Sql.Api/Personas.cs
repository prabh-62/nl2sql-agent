using Nl2Sql.Data;

namespace Nl2Sql.Api;

/// <summary>
/// Demo personas instead of real sign-in. The browser only ever sends a persona ID; the server maps
/// it to a tenant scope, so a client can never ask for a brand or location ID directly.
/// </summary>
public sealed record Persona(string Id, string Name, string Description, TenantScope Scope, string TimeZone);

public static class Personas
{
    public const string HeaderName = "X-Persona";

    public static IReadOnlyList<Persona> All { get; } =
    [
        new("maple-owner", "Maple & Bean owner", "Sees all 4 Maple & Bean locations in the Toronto area.", TenantScope.Owner(1), "America/Toronto"),
        new("maple-queen-st", "Maple & Bean Queen St manager", "Sees only the Queen St location.", TenantScope.Manager(1, 101), "America/Toronto"),
        new("harbour-owner", "Harbour Roast owner", "Sees all 3 Harbour Roast locations in Vancouver.", TenantScope.Owner(2), "America/Vancouver"),
        new("copper-owner", "Copper Kettle owner", "Sees both Copper Kettle locations in Toronto.", TenantScope.Owner(3), "America/Toronto"),
    ];

    public static Persona? Find(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
}
