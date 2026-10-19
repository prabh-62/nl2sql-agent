CREATE TABLE dbo.Customers
(
    CustomerId bigint NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    BrandId int NOT NULL CONSTRAINT FK_Customers_Brands REFERENCES dbo.Brands (BrandId),
    HomeLocationId int NOT NULL CONSTRAINT FK_Customers_Locations REFERENCES dbo.Locations (LocationId),
    IsLoyaltyMember bit NOT NULL,
    JoinedOn date NULL
);
