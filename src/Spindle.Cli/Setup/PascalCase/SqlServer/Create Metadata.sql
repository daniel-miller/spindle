-- Create a table to store entity metadata.
CREATE TABLE metadata.TEntity (

  StorageStructure VARCHAR(20) NOT NULL,
  StorageSchema VARCHAR(30) NOT NULL,
  StorageTable VARCHAR(40) NOT NULL,
  StorageTableRename VARCHAR(40) NULL,
  StorageKey VARCHAR(80) NOT NULL,

  ComponentType VARCHAR(20) NOT NULL,
  ComponentName VARCHAR(30) NOT NULL,
  ComponentFeature VARCHAR(40) NOT NULL,

  EntityName VARCHAR(50) NOT NULL,

  CollectionSlug VARCHAR(50) NOT NULL,
  CollectionKey VARCHAR(60) NOT NULL,

  EntityId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY
);
GO

-- Create a table to store origin metadata.
CREATE TABLE metadata.TOrigin (
  OriginId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,

  OriginWhen DATETIMEOFFSET NOT NULL,
  OriginDescription VARCHAR(1000) NULL,
  OriginReason VARCHAR(1000) NULL,
  OriginSource VARCHAR(100) NULL,

  UserId UNIQUEIDENTIFIER NOT NULL,
  OrganizationId UNIQUEIDENTIFIER NOT NULL,

  ProxyAgent UNIQUEIDENTIFIER NULL,   -- User is impersonated by Agent
  ProxySubject UNIQUEIDENTIFIER NULL  -- User acts on behalf of Subject
);
GO

-- Create views for schema, table, and column metadata on base tables.
CREATE VIEW metadata.VSchema
AS
SELECT 
  name AS SchemaName,
  (
    SELECT COUNT(*)
    FROM sys.objects
    WHERE objects.schema_id = schemas.schema_id
  ) AS ObjectCount,
  (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = name
          AND TABLE_TYPE = 'BASE TABLE'
  ) AS TableCount
FROM sys.schemas
WHERE name NOT LIKE 'db_%'
      AND name NOT IN ('guest', 'INFORMATION_SCHEMA', 'sys');
GO

CREATE VIEW metadata.VTable
AS
SELECT 
  SCHEMA_NAME(SYSTBL.schema_id) AS SchemaName,
  SYSTBL.name AS TableName,
  SYSTBL.max_column_id_used AS ColumnCount,
  CAST(CASE SINDX_1.index_id
           WHEN 1 THEN 1
           ELSE 0
       END AS BIT) AS HasClusteredIndex,
  COALESCE((
      SELECT SUM(rows)
      FROM sys.partitions AS SPART
      WHERE object_id = SYSTBL.object_id
            AND index_id < 2
    ), 0) AS RowCount,
  SYSTBL.create_date AS CreatedWhen,
  SYSTBL.modify_date AS ModifiedWhen
FROM sys.tables AS SYSTBL
INNER JOIN sys.indexes AS SINDX_1
  ON SINDX_1.object_id = SYSTBL.object_id AND SINDX_1.index_id < 2;
GO

CREATE VIEW metadata.VTableColumn
AS
SELECT 
  T.SchemaName,
  T.TableName,
  COLUMN_NAME AS ColumnName,
  DATA_TYPE AS DataType,
  CHARACTER_MAXIMUM_LENGTH AS MaximumLength,
  CAST(CASE
           WHEN IS_NULLABLE = 'YES' THEN 0
           WHEN IS_NULLABLE = 'NO' THEN 1
           ELSE NULL
       END AS BIT) AS IsRequired,
  CAST(CASE
           WHEN COLUMNPROPERTY(OBJECT_ID(T.SchemaName + '.' + T.TableName), COLUMN_NAME, 'IsIdentity') = 1 THEN 1
           ELSE 0
       END AS BIT) AS IsIdentity,
  C.ORDINAL_POSITION AS OrdinalPosition,
  C.COLUMN_DEFAULT AS DefaultValue
FROM INFORMATION_SCHEMA.COLUMNS AS C
INNER JOIN metadata.VTable AS T
  ON C.TABLE_NAME = T.TableName AND C.TABLE_SCHEMA = T.SchemaName;
GO

-- Only include primary key constraints in the Primary Key view.
CREATE VIEW metadata.VPrimaryKey
AS
SELECT 
  SS.name AS SchemaName,
  ST.name AS TableName,
  SC.name AS ColumnName,
  SKC.name AS ConstraintName,
  CAST(STY.name AS VARCHAR(20)) + '(' + 
    CAST(CASE STY.name
             WHEN 'NVARCHAR' THEN (SC.max_length / 2)
             ELSE SC.max_length
         END AS VARCHAR(20)) + ')' AS DataType,
  SC.is_identity AS IsIdentity
FROM sys.key_constraints AS SKC
INNER JOIN sys.tables AS ST ON ST.object_id = SKC.parent_object_id
INNER JOIN sys.schemas AS SS ON SS.schema_id = ST.schema_id
INNER JOIN sys.index_columns AS SIC ON SIC.object_id = ST.object_id AND SIC.index_id = SKC.unique_index_id
INNER JOIN sys.columns AS SC ON SC.object_id = ST.object_id AND SC.column_id = SIC.column_id
INNER JOIN sys.types AS STY ON SC.user_type_id = STY.user_type_id
WHERE SKC.type = 'PK';
GO
