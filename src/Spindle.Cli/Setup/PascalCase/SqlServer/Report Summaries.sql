-- Component types
SELECT
    ComponentType,
    STRING_AGG(ComponentName, ', ') WITHIN GROUP (ORDER BY ComponentName) AS Components,
    COUNT(*) AS Objects
FROM (
    SELECT DISTINCT
        ComponentType,
        ComponentName
    FROM metadata.TEntity
) AS UniqueComponents
GROUP BY ComponentType
ORDER BY ComponentType;

-- Components
SELECT ComponentType, ComponentName AS Component, COUNT(*) AS [Objects]
FROM metadata.TEntity
GROUP BY ComponentType, ComponentName
ORDER BY ComponentType, ComponentName;

-- Parts
WITH CTE AS (
    SELECT DISTINCT ComponentPart, ComponentName
    FROM metadata.TEntity
)
SELECT 
    ComponentPart,
    STRING_AGG(ComponentName, ', ') AS Components
FROM CTE
GROUP BY ComponentPart
ORDER BY ComponentPart;

-- Entities
WITH CTE AS (
    SELECT DISTINCT EntityName, ComponentName
    FROM metadata.TEntity
)
SELECT 
    EntityName AS Entity, 
    STRING_AGG(ComponentName, ', ') AS Components
FROM CTE
GROUP BY EntityName
ORDER BY EntityName;

-- Duplicate Entities
WITH CTE AS (
    SELECT DISTINCT EntityName, ComponentPart, ComponentName
    FROM metadata.TEntity
)
SELECT 
    EntityName AS DuplicateEntity,
    STRING_AGG(ComponentName, ', ') AS Components,
    COUNT(*) AS [Count]
FROM CTE
GROUP BY EntityName
HAVING COUNT(*) > 1
ORDER BY EntityName;

-- Endpoints
SELECT 
    'api/' + LOWER(ComponentName) + '/' + CollectionSlug AS ApiCollectionUrl, 
    COUNT(*) - 1 AS Duplicates
FROM metadata.TEntity
GROUP BY ComponentName, CollectionSlug
ORDER BY COUNT(*) desc, ComponentName, CollectionSlug;
