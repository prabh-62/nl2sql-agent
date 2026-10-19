-- Worked examples for the get_examples tool: a question and the SQL that answers it. Not tenant data,
-- so no row-level security; the queries themselves still run under it.
CREATE TABLE ai.Examples
(
    ExampleId int NOT NULL CONSTRAINT PK_Examples PRIMARY KEY,
    Question nvarchar(400) NOT NULL,
    [Sql] nvarchar(max) NOT NULL,
    Notes nvarchar(400) NULL
);
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Example questions with the SQL that answers them. Use as patterns, not as data.',
    @level0type = N'SCHEMA', @level0name = N'ai', @level1type = N'TABLE', @level1name = N'Examples';
