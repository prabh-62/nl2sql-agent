CREATE VIEW ai.MenuItems
AS
SELECT
    m.MenuItemId,
    m.Name,
    m.Category,
    m.Family,
    m.IsIced,
    CAST(CASE WHEN m.Category IN ('espresso', 'brewed') THEN 1 ELSE 0 END AS bit) AS IsCoffee,
    CAST(CASE WHEN m.Family = 'latte' THEN 1 ELSE 0 END AS bit) AS IsLatte,
    m.BasePrice
FROM dbo.MenuItems AS m;
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Menu items sold by the brand: drinks and food.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'MenuItems';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Menu item identifier.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'MenuItems',
    @level2type = N'COLUMN', @level2name = N'MenuItemId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Menu item name as shown to customers, for example "Iced Oat Latte".',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'MenuItems',
    @level2type = N'COLUMN', @level2name = N'Name';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'One of espresso, brewed, tea, food.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'MenuItems',
    @level2type = N'COLUMN', @level2name = N'Category';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Drink family, for example latte, cappuccino, americano, cold_brew, chai. Null for food. Hot and iced versions share a family.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'MenuItems',
    @level2type = N'COLUMN', @level2name = N'Family';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'1 for iced drinks.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'MenuItems',
    @level2type = N'COLUMN', @level2name = N'IsIced';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'1 when the item counts as coffee: categories espresso and brewed. Tea and food are not coffee.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'MenuItems',
    @level2type = N'COLUMN', @level2name = N'IsCoffee';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'1 for any latte, hot or iced. Use this for questions about lattes.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'MenuItems',
    @level2type = N'COLUMN', @level2name = N'IsLatte';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Price of the small size before modifiers, in the location''s currency (CAD), excluding tax.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'MenuItems',
    @level2type = N'COLUMN', @level2name = N'BasePrice';
