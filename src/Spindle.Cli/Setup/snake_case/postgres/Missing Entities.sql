-- Missing entities

with cte as (
                select 'Table'        as StorageStructure
                     , t.TABLE_SCHEMA as StorageSchema
                     , t.TABLE_NAME   as StorageTable
                     , (SELECT STRING_AGG(pk.column_name,',') FROM metadata.v_primary_key AS pk WHERE pk.schema_name = t.TABLE_SCHEMA AND pk.table_name = t.TABLE_NAME) AS StorageKey
                FROM INFORMATION_SCHEMA.TABLES AS t
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                UNION
                SELECT 'View'
                     , t.TABLE_SCHEMA
                     , t.TABLE_NAME
                     , '?'
                FROM INFORMATION_SCHEMA.TABLES AS t
                WHERE t.TABLE_TYPE <> 'BASE TABLE'
                UNION
                SELECT 'Procedure'
                     , ROUTINE_SCHEMA
                     , ROUTINE_NAME
                     , '?'
                FROM INFORMATION_SCHEMA.ROUTINES
                WHERE ROUTINE_TYPE = 'PROCEDURE'
            )
SELECT 
       cte.StorageStructure as storage_structure
     , cte.StorageSchema as storage_schema
     , cte.StorageTable as storage_table
     , null as storage_table_future
     , cte.StorageKey as storage_key
     , 1 + LENGTH(StorageKey) - LENGTH(REPLACE(StorageKey, ',', '')) AS storage_key_size
     , CASE 
       WHEN cte.StorageSchema IN ('integration') THEN 'Plugin'
       WHEN cte.StorageSchema IN ('bus','metadata','security') THEN 'Utility'
       ELSE 'Application'
       END AS component_type
     , cte.StorageSchema AS component_name
     , '?' AS component_feature
     , '?' AS entity_name
     , '?' AS collection_slug
     , '?' AS collection_key
     , gen_random_uuid() AS entity_id

FROM cte
WHERE     StorageSchema NOT IN ('information_schema', 'pg_catalog')
      AND StorageTable NOT IN ('t_entity')
      AND cte.StorageSchema NOT IN ('backups')
      AND cte.StorageTable NOT LIKE 'b%'
      AND cte.StorageTable NOT LIKE 'Temp%'
      AND cte.StorageTable NOT LIKE '%Buffer'
      AND NOT EXISTS (
                         SELECT *
                         FROM metadata.t_entity AS e
                         WHERE cte.StorageSchema = e.storage_schema
                               AND cte.StorageTable = e.storage_table
                     )
      AND cte.StorageStructure NOT IN ('Procedure','View')
order by StorageStructure
       , StorageSchema
       , StorageTable;