CREATE TABLE dbo.Refunds
(
    RefundId bigint NOT NULL CONSTRAINT PK_Refunds PRIMARY KEY,
    OrderId bigint NOT NULL CONSTRAINT FK_Refunds_Orders REFERENCES dbo.Orders (OrderId),
    BrandId int NOT NULL,
    LocationId int NOT NULL,
    -- Refunded amount, excluding tax.
    Amount decimal(9, 2) NOT NULL,
    RefundedAtUtc datetime2(0) NOT NULL,
    Reason varchar(40) NOT NULL
);
GO

CREATE INDEX IX_Refunds_OrderId ON dbo.Refunds (OrderId);
