-- The model only ever queries the curated views in schema ai.
CREATE SCHEMA ai;
GO

-- Row-level security objects live apart from the data they protect.
CREATE SCHEMA security;
