CREATE VIEW ai.OrderLines
AS
SELECT
    ol.OrderLineId,
    ol.OrderId,
    o.LocationId,
    l.Name AS LocationName,
    o.Channel,
    CAST(t.PlacedAtLocal AS date) AS LocalDate,
    DATEPART(hour, t.PlacedAtLocal) AS LocalHour,
    m.MenuItemId,
    m.Name AS MenuItemName,
    m.Category,
    m.Family,
    m.IsIced,
    CAST(CASE WHEN m.Category IN ('espresso', 'brewed') THEN 1 ELSE 0 END AS bit) AS IsCoffee,
    CAST(CASE WHEN m.Family = 'latte' THEN 1 ELSE 0 END AS bit) AS IsLatte,
    s.Name AS Size,
    ol.Quantity,
    milk.MilkType,
    CAST(ol.Quantity * (ol.UnitPrice + ISNULL(mods.ModifierPrice, 0)) AS decimal(9, 2)) AS LineRevenue,
    CAST(ol.Quantity * (ol.UnitPrice + ISNULL(mods.ModifierPrice, 0))
         * CASE WHEN p.Subtotal = 0 THEN 1 ELSE 1 - ISNULL(r.RefundedAmount, 0) / p.Subtotal END AS decimal(9, 2)) AS LineNetRevenue
FROM dbo.OrderLines AS ol
JOIN dbo.Orders AS o ON o.OrderId = ol.OrderId
JOIN dbo.Locations AS l ON l.LocationId = o.LocationId
JOIN dbo.MenuItems AS m ON m.MenuItemId = ol.MenuItemId
JOIN dbo.Payments AS p ON p.OrderId = o.OrderId
LEFT JOIN dbo.Sizes AS s ON s.SizeId = ol.SizeId
OUTER APPLY (SELECT SUM(olm.Price) AS ModifierPrice FROM dbo.OrderLineModifiers AS olm WHERE olm.OrderLineId = ol.OrderLineId) AS mods
OUTER APPLY (
    SELECT TOP (1) md.Name AS MilkType
    FROM dbo.OrderLineModifiers AS olm
    JOIN dbo.Modifiers AS md ON md.ModifierId = olm.ModifierId
    WHERE olm.OrderLineId = ol.OrderLineId AND md.Kind = 'milk'
) AS milk
OUTER APPLY (SELECT SUM(rf.Amount) AS RefundedAmount FROM dbo.Refunds AS rf WHERE rf.OrderId = o.OrderId) AS r
CROSS APPLY (SELECT CONVERT(datetime2(0), (o.PlacedAtUtc AT TIME ZONE 'UTC') AT TIME ZONE l.TimeZoneWindows) AS PlacedAtLocal) AS t
WHERE o.Status = 'completed';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Items on completed orders, one row per order line. Voided and cancelled orders are excluded. Use for questions about drinks, items, sizes and milk.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Order line identifier.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'OrderLineId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Order this line belongs to. Join to ai.Orders.OrderId.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'OrderId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Location where the order was placed.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'LocationId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Name of the location where the order was placed.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'LocationName';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'How the order was placed: mobile, in_store or delivery.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'Channel';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Local calendar date of the order.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'LocalDate';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Local hour of day the order was placed, 0 to 23.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'LocalHour';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Menu item ordered. Join to ai.MenuItems.MenuItemId.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'MenuItemId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Name of the menu item ordered.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'MenuItemName';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'One of espresso, brewed, tea, food.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'Category';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Drink family, for example latte, cappuccino, americano, macchiato, cold_brew, iced_capp, chai. Null for food.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'Family';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'1 for iced drinks.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'IsIced';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'1 when the item counts as coffee: categories espresso and brewed.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'IsCoffee';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'1 for any latte, hot or iced.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'IsLatte';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'small, medium or large. Null for items without sizes (food).',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'Size';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Number of items on this line.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'Quantity';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Milk chosen: whole, skim, oat, almond or soy. Null when no milk modifier was chosen (default milk or no milk).',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'MilkType';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Quantity times item price including size and modifiers, excluding tax and tips, before refunds.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'LineRevenue';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'LineRevenue after the order''s refunds are spread proportionally over its lines. Use for net revenue by item.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'OrderLines',
    @level2type = N'COLUMN', @level2name = N'LineNetRevenue';
