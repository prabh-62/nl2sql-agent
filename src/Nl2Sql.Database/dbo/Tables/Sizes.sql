CREATE TABLE dbo.Sizes
(
    SizeId int NOT NULL CONSTRAINT PK_Sizes PRIMARY KEY,
    Name varchar(20) NOT NULL CONSTRAINT UQ_Sizes_Name UNIQUE,
    PriceDelta decimal(9, 2) NOT NULL
);
