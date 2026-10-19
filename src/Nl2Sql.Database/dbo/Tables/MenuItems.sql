-- Check constraints are written the way SQL Server stores them (an IN list becomes reversed ORs);
-- otherwise DacFx sees a difference and recreates them on every deploy.
CREATE TABLE dbo.MenuItems
(
    MenuItemId int NOT NULL CONSTRAINT PK_MenuItems PRIMARY KEY,
    BrandId int NOT NULL CONSTRAINT FK_MenuItems_Brands REFERENCES dbo.Brands (BrandId),
    Name nvarchar(100) NOT NULL,
    Category varchar(20) NOT NULL CONSTRAINT CK_MenuItems_Category CHECK (Category = 'food' OR Category = 'tea' OR Category = 'brewed' OR Category = 'espresso'),
    -- Drink family, so "latte" covers hot and iced lattes alike. Null for food.
    Family varchar(30) NULL,
    IsIced bit NOT NULL,
    BasePrice decimal(9, 2) NOT NULL
);
