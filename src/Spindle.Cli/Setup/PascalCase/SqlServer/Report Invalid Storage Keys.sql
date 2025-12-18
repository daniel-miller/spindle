-- Invalid Storage Keys

WITH CTE AS (
    SELECT 
        'Table' AS StorageStructure,
        T.TABLE_SCHEMA AS StorageSchema,
        T.TABLE_NAME AS StorageTable,
        (
            SELECT STRING_AGG(PK.ColumnName, ',') 
                   WITHIN GROUP (ORDER BY ColumnName ASC)
            FROM databases.VPrimaryKey AS PK
            WHERE PK.SchemaName = T.TABLE_SCHEMA 
              AND PK.TableName = T.TABLE_NAME
        ) AS StorageKey
    FROM INFORMATION_SCHEMA.TABLES AS T
    WHERE T.TABLE_TYPE = 'BASE TABLE'
)

SELECT 
    CTE.StorageStructure,
    CTE.StorageSchema,
    CTE.StorageTable,
    CTE.StorageKey AS ActualKey,
    1 + LEN(CTE.StorageKey) - LEN(REPLACE(CTE.StorageKey, ',', '')) AS ActualKeySize,
    E.StorageKey AS EntityStorageKey
FROM CTE
INNER JOIN metadata.TEntity AS E 
    ON CTE.StorageSchema = E.StorageSchema 
   AND CTE.StorageTable = E.StorageTable
WHERE 
    CTE.StorageTable NOT IN ('TEntity', 'TOrganization') AND
    CTE.StorageSchema NOT IN ('backups') AND
    CTE.StorageTable NOT LIKE 'b%' AND
    CTE.StorageTable NOT LIKE 'Temp%' AND
    CTE.StorageTable NOT LIKE '%Buffer' AND
    CTE.StorageKey <> E.StorageKey
ORDER BY 
    CTE.StorageStructure,
    CTE.StorageSchema,
    CTE.StorageTable;
