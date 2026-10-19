-- Session context is set once per connection with @read_only = 1:
--   BrandId    required; no brand means no rows.
--   LocationId 0 for a brand owner (whole brand), the location's ID for a location manager.
--              Required too: a missing key fails closed. (Not NULL for owners: a NULL sent as a
--              command parameter wasn't locked by @read_only in our tests, so it could be set later.)

CREATE FUNCTION security.fn_brand_access(@BrandId int)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS HasAccess
    WHERE @BrandId = CAST(SESSION_CONTEXT(N'BrandId') AS int);
GO

CREATE FUNCTION security.fn_location_access(@BrandId int, @LocationId int)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS HasAccess
    WHERE @BrandId = CAST(SESSION_CONTEXT(N'BrandId') AS int)
      AND (CAST(SESSION_CONTEXT(N'LocationId') AS int) = 0
           OR @LocationId = CAST(SESSION_CONTEXT(N'LocationId') AS int));
