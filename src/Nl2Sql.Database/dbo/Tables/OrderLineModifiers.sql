CREATE TABLE dbo.OrderLineModifiers
(
    OrderLineId bigint NOT NULL CONSTRAINT FK_OrderLineModifiers_OrderLines REFERENCES dbo.OrderLines (OrderLineId),
    ModifierId int NOT NULL CONSTRAINT FK_OrderLineModifiers_Modifiers REFERENCES dbo.Modifiers (ModifierId),
    BrandId int NOT NULL,
    LocationId int NOT NULL,
    -- Price of the modifier for one item.
    Price decimal(9, 2) NOT NULL,
    CONSTRAINT PK_OrderLineModifiers PRIMARY KEY (OrderLineId, ModifierId)
);
