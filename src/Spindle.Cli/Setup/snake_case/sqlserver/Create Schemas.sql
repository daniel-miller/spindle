DECLARE @schemas TABLE (SchemaName SYSNAME NOT NULL);

INSERT INTO @schemas (SchemaName)
VALUES

  -- Application
  ('assessment'),
  ('billing'),
  ('calendar'),
  ('contact'),
  ('content'),
  ('job'),
  ('learning'),
  ('message'),
  ('record'),
  ('reporting'),
  ('standard'),
  ('survey'),
  ('workflow'),

  -- Plugin
  ('extension'),
  ('integration'),

  -- Utility
  ('metadata'),
  ('security'),
  ('timeline');

DECLARE @script NVARCHAR(MAX);

SELECT @script = STRING_AGG(
   'IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = ''' + SchemaName + ''')
    BEGIN
        EXEC(''CREATE SCHEMA [' + SchemaName + ']'')
    END',
    CHAR(13) + CHAR(10)
)
FROM @schemas;

EXEC sp_executesql @sql = @script;
