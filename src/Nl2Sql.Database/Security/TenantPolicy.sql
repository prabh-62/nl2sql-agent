CREATE SECURITY POLICY security.TenantPolicy
    ADD FILTER PREDICATE security.fn_brand_access(BrandId) ON dbo.Brands,
    ADD FILTER PREDICATE security.fn_brand_access(BrandId) ON dbo.MenuItems,
    ADD FILTER PREDICATE security.fn_location_access(BrandId, LocationId) ON dbo.Locations,
    ADD FILTER PREDICATE security.fn_location_access(BrandId, HomeLocationId) ON dbo.Customers,
    ADD FILTER PREDICATE security.fn_location_access(BrandId, LocationId) ON dbo.Orders,
    ADD FILTER PREDICATE security.fn_location_access(BrandId, LocationId) ON dbo.OrderLines,
    ADD FILTER PREDICATE security.fn_location_access(BrandId, LocationId) ON dbo.OrderLineModifiers,
    ADD FILTER PREDICATE security.fn_location_access(BrandId, LocationId) ON dbo.Payments,
    ADD FILTER PREDICATE security.fn_location_access(BrandId, LocationId) ON dbo.Refunds
WITH (STATE = ON, SCHEMABINDING = ON);
