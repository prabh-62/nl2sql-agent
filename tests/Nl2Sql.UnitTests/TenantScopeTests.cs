using Nl2Sql.Data;

namespace Nl2Sql.UnitTests;

public sealed class TenantScopeTests
{
    [Fact]
    public void Owner_has_no_location() => Assert.Null(TenantScope.Owner(1).LocationId);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Brand_must_be_positive(int brandId) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TenantScope.Owner(brandId));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Location_must_be_positive_because_0_means_the_whole_brand(int locationId) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TenantScope.Manager(1, locationId));
}
