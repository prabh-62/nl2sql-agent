-- Check constraints are written the way SQL Server stores them (an IN list becomes reversed ORs);
-- otherwise DacFx sees a difference and recreates them on every deploy.
CREATE TABLE dbo.Orders
(
    OrderId bigint NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    BrandId int NOT NULL CONSTRAINT FK_Orders_Brands REFERENCES dbo.Brands (BrandId),
    LocationId int NOT NULL CONSTRAINT FK_Orders_Locations REFERENCES dbo.Locations (LocationId),
    -- Null for guest checkouts.
    CustomerId bigint NULL CONSTRAINT FK_Orders_Customers REFERENCES dbo.Customers (CustomerId),
    Channel varchar(10) NOT NULL CONSTRAINT CK_Orders_Channel CHECK (Channel = 'delivery' OR Channel = 'in_store' OR Channel = 'mobile'),
    Status varchar(10) NOT NULL CONSTRAINT CK_Orders_Status CHECK (Status = 'cancelled' OR Status = 'voided' OR Status = 'completed'),
    PlacedAtUtc datetime2(0) NOT NULL
);
GO

CREATE INDEX IX_Orders_Location_PlacedAt ON dbo.Orders (BrandId, LocationId, PlacedAtUtc) INCLUDE (Status, Channel, CustomerId);
