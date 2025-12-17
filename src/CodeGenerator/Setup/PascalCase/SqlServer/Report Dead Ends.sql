-- Dead Ends

SELECT 
    E.StorageSchema,
    E.StorageTable
FROM metadata.TEntity AS E
WHERE NOT EXISTS (
    SELECT *
    FROM databases.VTable AS T
    WHERE T.SchemaName = E.StorageSchema
      AND T.TableName = E.StorageTable
)
ORDER BY 
    E.StorageSchema,
    E.StorageTable;

-- Missing Entities

WITH CTE AS (
    SELECT 
        'Table' AS StorageStructure,
        T.TABLE_SCHEMA AS StorageSchema,
        T.TABLE_NAME AS StorageTable,
        (
            SELECT STRING_AGG(PK.ColumnName, ',') 
            FROM metadata.VPrimaryKey AS PK 
            WHERE PK.SchemaName = T.TABLE_SCHEMA 
              AND PK.TableName = T.TABLE_NAME
        ) AS StorageKey
    FROM INFORMATION_SCHEMA.TABLES AS T
    WHERE T.TABLE_TYPE = 'BASE TABLE'

    UNION

    SELECT 
        'View',
        T.TABLE_SCHEMA,
        T.TABLE_NAME,
        '?'
    FROM INFORMATION_SCHEMA.TABLES AS T
    WHERE T.TABLE_TYPE <> 'BASE TABLE'

    UNION

    SELECT 
        'Procedure',
        ROUTINE_SCHEMA,
        ROUTINE_NAME,
        '?'
    FROM INFORMATION_SCHEMA.ROUTINES
    WHERE ROUTINE_TYPE = 'PROCEDURE'
)

SELECT 
    CTE.StorageStructure,
    CTE.StorageSchema,
    CTE.StorageTable,
    null AS StorageTableRename,
    CTE.StorageKey,
    CASE 
        WHEN CTE.StorageSchema IN ('integration') THEN 'Plugin'
        ELSE 'Application'
    END AS ComponentType,
    CTE.StorageSchema AS ComponentName,
    '?' AS ComponentFeature,
    '?' AS EntityName,
    '?' AS CollectionSlug,
    '?' AS CollectionKey,
    NEWID() AS EntityId

FROM CTE
WHERE NOT EXISTS (
    SELECT *
    FROM metadata.TEntity AS E
    WHERE CTE.StorageSchema = E.StorageSchema
      AND CTE.StorageTable = E.StorageTable
)
AND CTE.StorageStructure NOT IN ('Procedure', 'View')
AND CTE.StorageSchema NOT IN ('backups', 'custom_cmds', 'dbo')
AND CTE.StorageTable NOT LIKE '%Buffer'
AND CTE.StorageTable NOT LIKE 'B%'
AND CTE.StorageTable NOT IN ('TOrganization')
ORDER BY 
    StorageStructure,
    StorageSchema,
    StorageTable;
