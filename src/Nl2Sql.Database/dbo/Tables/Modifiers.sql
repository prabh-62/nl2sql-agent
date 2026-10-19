-- Check constraints are written the way SQL Server stores them (an IN list becomes reversed ORs);
-- otherwise DacFx sees a difference and recreates them on every deploy.
CREATE TABLE dbo.Modifiers
(
    ModifierId int NOT NULL CONSTRAINT PK_Modifiers PRIMARY KEY,
    Name varchar(40) NOT NULL CONSTRAINT UQ_Modifiers_Name UNIQUE,
    Kind varchar(20) NOT NULL CONSTRAINT CK_Modifiers_Kind CHECK (Kind = 'extra_shot' OR Kind = 'syrup' OR Kind = 'milk'),
    Price decimal(9, 2) NOT NULL
);
