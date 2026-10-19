namespace Nl2Sql.Data;

/// <summary>
/// Whose data a query may see. A brand owner sees the whole brand (no location);
/// a location manager sees one location.
/// </summary>
public sealed record TenantScope
{
    public TenantScope(int brandId, int? locationId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(brandId);
        if (locationId is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(locationId.Value, nameof(locationId));
        }

        BrandId = brandId;
        LocationId = locationId;
    }

    public int BrandId { get; }

    /// <summary>Null for a brand owner.</summary>
    public int? LocationId { get; }

    public static TenantScope Owner(int brandId) => new(brandId);

    public static TenantScope Manager(int brandId, int locationId) => new(brandId, locationId);
}
