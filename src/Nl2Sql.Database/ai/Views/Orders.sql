CREATE VIEW ai.Orders
AS
SELECT
    o.OrderId,
    o.LocationId,
    l.Name AS LocationName,
    o.CustomerId,
    CAST(CASE WHEN o.CustomerId IS NULL THEN 1 ELSE 0 END AS bit) AS IsGuest,
    CAST(ISNULL(c.IsLoyaltyMember, 0) AS bit) AS IsLoyaltyMember,
    o.Channel,
    o.PlacedAtUtc,
    t.PlacedAtLocal,
    CAST(t.PlacedAtLocal AS date) AS LocalDate,
    DATEPART(hour, t.PlacedAtLocal) AS LocalHour,
    DATENAME(weekday, t.PlacedAtLocal) AS LocalDayOfWeek,
    p.Subtotal,
    p.Tax,
    p.Tip,
    ISNULL(r.RefundedAmount, 0) AS RefundedAmount,
    p.Subtotal - ISNULL(r.RefundedAmount, 0) AS NetRevenue
FROM dbo.Orders AS o
JOIN dbo.Locations AS l ON l.LocationId = o.LocationId
JOIN dbo.Payments AS p ON p.OrderId = o.OrderId
LEFT JOIN dbo.Customers AS c ON c.CustomerId = o.CustomerId
OUTER APPLY (SELECT SUM(rf.Amount) AS RefundedAmount FROM dbo.Refunds AS rf WHERE rf.OrderId = o.OrderId) AS r
CROSS APPLY (SELECT CONVERT(datetime2(0), (o.PlacedAtUtc AT TIME ZONE 'UTC') AT TIME ZONE l.TimeZoneWindows) AS PlacedAtLocal) AS t
WHERE o.Status = 'completed';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Completed orders, one row per order. Voided and cancelled orders are excluded. Revenue columns exclude tax and tips; NetRevenue is after refunds.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Order identifier.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'OrderId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Location where the order was placed.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'LocationId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Name of the location where the order was placed.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'LocationName';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Customer account that placed the order, whether or not it is a loyalty member. Null for guest checkouts. For loyalty members, also filter IsLoyaltyMember = 1.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'CustomerId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'1 for guest checkouts (no customer account).',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'IsGuest';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'1 when the order was placed by a loyalty member.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'IsLoyaltyMember';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'How the order was placed: mobile, in_store or delivery.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'Channel';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'When the order was placed, in UTC.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'PlacedAtUtc';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'When the order was placed, in the location''s local time. Use this, LocalDate and LocalHour for date and time questions.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'PlacedAtLocal';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Local calendar date of the order. Use for "last week", "last month", "this quarter".',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'LocalDate';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Local hour of day the order was placed, 0 to 23.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'LocalHour';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Local day of the week, for example Monday.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'LocalDayOfWeek';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Order amount before tax and tip, including modifiers.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'Subtotal';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Sales tax charged. Not revenue.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'Tax';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Tip left by the customer. Not revenue.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'Tip';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Total refunded on this order, excluding tax. 0 when not refunded.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'RefundedAmount';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Revenue for the order: Subtotal minus refunds, excluding tax and tips. Use this for revenue questions.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Orders',
    @level2type = N'COLUMN', @level2name = N'NetRevenue';
