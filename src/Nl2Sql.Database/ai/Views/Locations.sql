CREATE VIEW ai.Locations
AS
SELECT
    l.LocationId,
    b.Name AS BrandName,
    l.Name AS LocationName,
    l.City,
    l.TimeZoneIana AS TimeZone,
    l.Currency,
    l.OpenedOn
FROM dbo.Locations AS l
JOIN dbo.Brands AS b ON b.BrandId = l.BrandId;
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Café locations the current user can see, one row per location.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Locations';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Location identifier. Join key for LocationId in the other views.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Locations',
    @level2type = N'COLUMN', @level2name = N'LocationId';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Name of the café brand.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Locations',
    @level2type = N'COLUMN', @level2name = N'BrandName';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Name of the location, for example "Queen St W".',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Locations',
    @level2type = N'COLUMN', @level2name = N'LocationName';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'City the location is in.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Locations',
    @level2type = N'COLUMN', @level2name = N'City';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'IANA time zone of the location, for example America/Toronto. All Local* columns in other views use it.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Locations',
    @level2type = N'COLUMN', @level2name = N'TimeZone';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Currency of every price and amount at this location: CAD for Canadian locations, USD for US locations.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Locations',
    @level2type = N'COLUMN', @level2name = N'Currency';
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Date the location opened.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'VIEW', @level1name = N'Locations',
    @level2type = N'COLUMN', @level2name = N'OpenedOn';
