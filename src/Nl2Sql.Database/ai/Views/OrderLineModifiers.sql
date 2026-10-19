CREATE VIEW ai.OrderLineModifiers
AS
SELECT
    olm.OrderLineId,
    ol.OrderId,
    o.LocationId,
    CAST(t.PlacedAtLocal AS date) AS LocalDate,
    md.Name AS ModifierName,
    md.Kind,
    olm.Price
FROM dbo.OrderLineModifiers AS olm
JOIN dbo.OrderLines AS ol ON ol.OrderLineId = olm.OrderLineId
JOIN dbo.Orders AS o ON o.OrderId = ol.OrderId
JOIN dbo.Locations AS l ON l.LocationId = o.LocationId
JOIN dbo.Modifiers AS md ON md.ModifierId = olm.ModifierId
CROSS APPLY (SELECT CONVERT(datetime2(0), (o.PlacedAtUtc AT TIME ZONE 'UTC') AT TIME ZONE l.TimeZoneWindows) AS PlacedAtLocal) AS t
WHERE o.Status = 'completed';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Modifiers added to items on completed orders, such as milk choice, syrups and extra shots. One row per modifier per order line.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLineModifiers';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Order line the modifier was added to. Join to ai.OrderLines.OrderLineId.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLineModifiers',
    @level2type = N'COLUMN', @level2name = N'OrderLineId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Order the line belongs to.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLineModifiers',
    @level2type = N'COLUMN', @level2name = N'OrderId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Location where the order was placed.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLineModifiers',
    @level2type = N'COLUMN', @level2name = N'LocationId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Local calendar date of the order.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLineModifiers',
    @level2type = N'COLUMN', @level2name = N'LocalDate';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Modifier name, for example oat, vanilla, extra_shot.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLineModifiers',
    @level2type = N'COLUMN', @level2name = N'ModifierName';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'milk, syrup or extra_shot.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLineModifiers',
    @level2type = N'COLUMN', @level2name = N'Kind';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Price of the modifier for one item, excluding tax.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLineModifiers',
    @level2type = N'COLUMN', @level2name = N'Price';
