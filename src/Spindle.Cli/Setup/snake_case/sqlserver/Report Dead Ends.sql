-- Dead ends

SELECT e.storage_schema
     , e.storage_table
from metadata.t_entity as e
where not exists (
                     select *
                     from databases.VTable as t
                     where t.SchemaName = e.storage_schema
                           and t.TableName = e.storage_table
                 )
order by e.storage_schema
       , e.storage_table;

-- Missing entities

with cte as (
                select 'Table'        as storage_structure
                     , t.TABLE_SCHEMA as storage_schema
                     , t.TABLE_NAME   as storage_table
                     , (SELECT STRING_AGG(pk.column_name,',') FROM metadata.v_primary_key AS pk WHERE pk.schema_name = t.TABLE_SCHEMA AND pk.table_name = t.TABLE_NAME) AS storage_key
                FROM INFORMATION_SCHEMA.TABLES AS t
                WHERE t.TABLE_TYPE = 'Base Table'
                UNION
                SELECT 'View'
                     , t.TABLE_SCHEMA
                     , t.TABLE_NAME
                     , '?'
                FROM INFORMATION_SCHEMA.TABLES AS t
                WHERE t.TABLE_TYPE <> 'Base Table'
                UNION
                SELECT 'Procedure'
                     , ROUTINE_SCHEMA
                     , ROUTINE_NAME
                     , '?'
                FROM INFORMATION_SCHEMA.ROUTINES
                WHERE ROUTINE_TYPE = 'PROCEDURE'
            )
SELECT 
       CASE 
       WHEN cte.storage_schema IN ('integration') THEN 'Plugin'
       ELSE 'Application'
       END AS component_type
     , cte.storage_schema AS component_name
     , '?' AS component_feature
     , NEWID() AS entity_id
     , '?' AS entity_name
     , '?' AS collection_slug
     , '?' AS collection_key
     , cte.storage_structure
     , cte.storage_schema
     , cte.storage_table
     , cte.storage_key
     , 1 + LEN(storage_key) - LEN(REPLACE(storage_key, ',', '')) AS storage_key_size

FROM cte
WHERE NOT EXISTS (
                         SELECT *
                         FROM metadata.t_entity AS e
                         WHERE cte.storage_schema = e.storage_schema
                               AND cte.storage_table = e.storage_table
                     )
      AND cte.storage_structure NOT IN ('Procedure','View')
      AND cte.storage_schema NOT IN ('backups','custom_cmds','dbo')
      AND cte.storage_table NOT LIKE '%Buffer'
      AND cte.storage_table NOT LIKE 'B%'
      AND cte.storage_table NOT IN ('TOrganization')
order by storage_structure
       , storage_schema
       , storage_table;