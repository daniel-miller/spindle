-- Invalid storage keys

with cte as (
                select 'Table'        as storage_structure
                     , t.TABLE_SCHEMA as storage_schema
                     , t.TABLE_NAME   as storage_table
                     , (SELECT STRING_AGG(pk.ColumnName,',') WITHIN GROUP (ORDER BY ColumnName ASC) FROM databases.VPrimaryKey AS pk WHERE pk.SchemaName = t.TABLE_SCHEMA AND pk.TableName = t.TABLE_NAME) AS storage_key
                FROM INFORMATION_SCHEMA.TABLES AS t
                WHERE t.TABLE_TYPE = 'Base Table'
                
            )
SELECT 
       cte.storage_structure
     , cte.storage_schema
     , cte.storage_table
     , cte.storage_key AS actual_key
     , 1 + LEN(cte.storage_key) - LEN(REPLACE(cte.storage_key, ',', '')) AS actual_key_size
     , e.storage_key AS entity_storage_key
     
FROM cte

INNER JOIN metadata.t_entity AS e ON cte.storage_schema = e.storage_schema AND cte.storage_table = e.storage_table

WHERE cte.storage_table NOT IN ( 'TEntity', 'TOrganization' )
      AND cte.storage_schema NOT IN ('backups')
      AND cte.storage_table NOT LIKE 'b%'
      AND cte.storage_table NOT LIKE 'Temp%'
      AND cte.storage_table NOT LIKE '%Buffer'
      
      AND cte.storage_key <> e.storage_key

ORDER BY cte.storage_structure
       , cte.storage_schema
       , cte.storage_table;