CREATE VIEW ai.Refunds
AS
SELECT
    rf.RefundId,
    rf.OrderId,
    o.LocationId,
    l.Name AS LocationName,
    o.Channel,
    rf.Amount,
    rf.RefundedAtUtc,
    t.RefundedAtLocal,
    CAST(t.RefundedAtLocal AS date) AS LocalDate,
    rf.Reason
FROM dbo.Refunds AS rf
JOIN dbo.Orders AS o ON o.OrderId = rf.OrderId
JOIN dbo.Locations AS l ON l.LocationId = o.LocationId
CROSS APPLY (SELECT CONVERT(datetime2(0), (rf.RefundedAtUtc AT TIME ZONE 'UTC') AT TIME ZONE l.TimeZoneWindows) AS RefundedAtLocal) AS t
WHERE o.Status = 'completed';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Refunds on completed orders, one row per refund. Amounts exclude tax.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Refund identifier.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds',
    @level2type = N'COLUMN', @level2name = N'RefundId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Refunded order. Join to ai.Orders.OrderId.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds',
    @level2type = N'COLUMN', @level2name = N'OrderId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Location where the order was placed.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds',
    @level2type = N'COLUMN', @level2name = N'LocationId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Name of the location where the order was placed.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds',
    @level2type = N'COLUMN', @level2name = N'LocationName';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'How the refunded order was placed: mobile, in_store or delivery.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds',
    @level2type = N'COLUMN', @level2name = N'Channel';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Refunded amount, excluding tax.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds',
    @level2type = N'COLUMN', @level2name = N'Amount';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'When the refund was issued, in UTC.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds',
    @level2type = N'COLUMN', @level2name = N'RefundedAtUtc';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'When the refund was issued, in the location''s local time.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds',
    @level2type = N'COLUMN', @level2name = N'RefundedAtLocal';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Local calendar date of the refund.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds',
    @level2type = N'COLUMN', @level2name = N'LocalDate';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Why the order was refunded, for example wrong_item, quality, late_delivery.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Refunds',
    @level2type = N'COLUMN', @level2name = N'Reason';
