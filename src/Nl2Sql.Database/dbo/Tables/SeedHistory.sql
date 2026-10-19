-- Records which deterministic seed has been loaded. Not a tenant table, so no row-level security.
CREATE TABLE dbo.SeedHistory
(
    SeedVersion int NOT NULL CONSTRAINT PK_SeedHistory PRIMARY KEY,
    AnchorDate date NOT NULL,
    OrderCount int NOT NULL,
    SeededAtUtc datetime2(0) NOT NULL
);
