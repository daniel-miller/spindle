DECLARE @schemas TABLE (
    [SchemaName] SYSNAME NOT NULL
);

INSERT INTO @schemas ([SchemaName])
VALUES ('achievement') -- Feature (App)
     , ('assessment')
     , ('billing')
     , ('calendar')
     , ('contact')
     , ('content')
     , ('course')
     , ('gradebook')
     , ('job')
     , ('location')
     , ('logbook')
     , ('message')
     , ('report')
     , ('site')
     , ('standard')
     , ('survey')
     , ('workflow')

     , ('integration') -- Plugin
     , ('variant')

     , ('metadata')    -- Utility
     , ('security')
     , ('setup')
     , ('timeline');

DECLARE @script NVARCHAR(MAX);

SELECT
    @script = STRING_AGG('IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = ''' + [SchemaName] + ''')
    BEGIN
        EXEC(''CREATE SCHEMA [' + [SchemaName] + ']'')
    END', CHAR(13) + CHAR(10))
FROM
    @schemas;

EXEC [sp_executesql] @sql = @script;
