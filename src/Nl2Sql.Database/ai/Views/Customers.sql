CREATE VIEW ai.Customers
AS
SELECT
    c.CustomerId,
    c.HomeLocationId,
    l.Name AS HomeLocationName,
    c.JoinedOn,
    stats.CompletedOrders,
    stats.LastOrderLocalDate
FROM dbo.Customers AS c
JOIN dbo.Locations AS l ON l.LocationId = c.HomeLocationId
OUTER APPLY (
    SELECT COUNT(*) AS CompletedOrders, MAX(ao.LocalDate) AS LastOrderLocalDate
    FROM ai.Orders AS ao
    WHERE ao.CustomerId = c.CustomerId
) AS stats
WHERE c.IsLoyaltyMember = 1;
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Loyalty members, one row per member. "Customers" means loyalty members; guest checkouts have no customer record (see ai.Orders.IsGuest).',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Customers';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Loyalty member identifier. Join to ai.Orders.CustomerId.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Customers',
    @level2type = N'COLUMN', @level2name = N'CustomerId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Location the member signed up at.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Customers',
    @level2type = N'COLUMN', @level2name = N'HomeLocationId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Name of the location the member signed up at.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Customers',
    @level2type = N'COLUMN', @level2name = N'HomeLocationName';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Date the member joined the loyalty program.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Customers',
    @level2type = N'COLUMN', @level2name = N'JoinedOn';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Number of completed orders by this member that the current user can see.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Customers',
    @level2type = N'COLUMN', @level2name = N'CompletedOrders';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Local date of the member''s most recent completed order that the current user can see. Null if none.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Customers',
    @level2type = N'COLUMN', @level2name = N'LastOrderLocalDate';
