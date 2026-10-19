using Nl2Sql.Agent;

namespace Nl2Sql.UnitTests;

public sealed class PersonaTests
{
    [Fact]
    public void Persona_ids_are_unique() =>
        Assert.Equal(Personas.All.Count, Personas.All.Select(p => p.Id).Distinct().Count());

    [Fact]
    public void Every_persona_has_a_valid_time_zone() =>
        Assert.All(Personas.All, p => Assert.NotNull(TimeZoneInfo.FindSystemTimeZoneById(p.TimeZone)));

    [Fact]
    public void There_is_a_manager_for_testing_isolation() =>
        Assert.Contains(Personas.All, p => p.Scope.LocationId is not null);
}
