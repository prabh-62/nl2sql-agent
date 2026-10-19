-- Everything the agent may do: read the curated views. No dbo, no writes, no EXECUTE.
CREATE ROLE nl2sql_reader;
GO

GRANT SELECT ON SCHEMA::ai TO nl2sql_reader;
GO

-- Local development connects as the developer (an Entra admin) and switches to this user for each
-- query (EXECUTE AS USER ... WITH COOKIE), so local runs get exactly the reader's permissions.
-- Nobody can sign in as it. In Azure and CI the app signs in as the nl2sql-reader managed identity,
-- which the migration service adds to the same role.
CREATE USER nl2sql_reader_dev WITHOUT LOGIN;
GO

-- DacFx doesn't grant CONNECT on its own, and EXECUTE AS USER fails without it.
GRANT CONNECT TO nl2sql_reader_dev;
GO

ALTER ROLE nl2sql_reader ADD MEMBER nl2sql_reader_dev;
