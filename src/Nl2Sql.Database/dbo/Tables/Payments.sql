CREATE TABLE dbo.Payments
(
    PaymentId bigint NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    OrderId bigint NOT NULL CONSTRAINT FK_Payments_Orders REFERENCES dbo.Orders (OrderId) CONSTRAINT UQ_Payments_OrderId UNIQUE,
    BrandId int NOT NULL,
    LocationId int NOT NULL,
    Subtotal decimal(9, 2) NOT NULL,
    Tax decimal(9, 2) NOT NULL,
    Tip decimal(9, 2) NOT NULL
);
