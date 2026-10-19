CREATE TABLE dbo.Brands
(
    BrandId int NOT NULL CONSTRAINT PK_Brands PRIMARY KEY,
    Name nvarchar(100) NOT NULL CONSTRAINT UQ_Brands_Name UNIQUE
);
