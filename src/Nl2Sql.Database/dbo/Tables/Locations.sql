CREATE TABLE dbo.Locations
(
    LocationId int NOT NULL CONSTRAINT PK_Locations PRIMARY KEY,
    BrandId int NOT NULL CONSTRAINT FK_Locations_Brands REFERENCES dbo.Brands (BrandId),
    Name nvarchar(100) NOT NULL,
    City nvarchar(100) NOT NULL,
    -- IANA name for application code; Windows name for SQL Server's AT TIME ZONE.
    TimeZoneIana varchar(64) NOT NULL,
    TimeZoneWindows varchar(64) NOT NULL,
    OpenedOn date NOT NULL,
    -- ISO 4217 code of the prices and amounts at this location. Added last, so deploying it is a
    -- plain ALTER TABLE: rebuilding a table behind row-level security is blocked by DacFx.
    Currency char(3) NOT NULL CONSTRAINT DF_Locations_Currency DEFAULT ('CAD')
);
