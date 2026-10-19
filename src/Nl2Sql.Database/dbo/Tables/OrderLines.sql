CREATE TABLE dbo.OrderLines
(
    OrderLineId bigint NOT NULL CONSTRAINT PK_OrderLines PRIMARY KEY,
    OrderId bigint NOT NULL CONSTRAINT FK_OrderLines_Orders REFERENCES dbo.Orders (OrderId),
    -- Tenant keys are repeated on every row so row-level security needs no joins.
    BrandId int NOT NULL,
    LocationId int NOT NULL,
    MenuItemId int NOT NULL CONSTRAINT FK_OrderLines_MenuItems REFERENCES dbo.MenuItems (MenuItemId),
    SizeId int NULL CONSTRAINT FK_OrderLines_Sizes REFERENCES dbo.Sizes (SizeId),
    Quantity int NOT NULL CONSTRAINT CK_OrderLines_Quantity CHECK (Quantity > 0),
    -- Price of one item including size, excluding modifiers.
    UnitPrice decimal(9, 2) NOT NULL
);
GO

CREATE INDEX IX_OrderLines_OrderId ON dbo.OrderLines (OrderId);
