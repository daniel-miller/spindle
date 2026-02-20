-- Component types
SELECT
    ComponentType,
    STRING_AGG(SubsystemName, ', ') WITHIN GROUP (ORDER BY SubsystemName) AS Components,
    COUNT(*) AS Objects
FROM (
    SELECT DISTINCT
        ComponentType,
        SubsystemName
    FROM metadata.TEntity
) AS UniqueComponents
GROUP BY ComponentType
ORDER BY ComponentType;

-- Components
SELECT ComponentType, SubsystemName AS Component, COUNT(*) AS [Objects]
FROM metadata.TEntity
GROUP BY ComponentType, SubsystemName
ORDER BY ComponentType, SubsystemName;

-- Parts
WITH CTE AS (
    SELECT DISTINCT ComponentPart, SubsystemName
    FROM metadata.TEntity
)
SELECT 
    ComponentPart,
    STRING_AGG(SubsystemName, ', ') AS Components
FROM CTE
GROUP BY ComponentPart
ORDER BY ComponentPart;

-- Entities
WITH CTE AS (
    SELECT DISTINCT EntityName, SubsystemName
    FROM metadata.TEntity
)
SELECT 
    EntityName AS Entity, 
    STRING_AGG(SubsystemName, ', ') AS Components
FROM CTE
GROUP BY EntityName
ORDER BY EntityName;

-- Duplicate Entities
WITH CTE AS (
    SELECT DISTINCT EntityName, ComponentPart, SubsystemName
    FROM metadata.TEntity
)
SELECT 
    EntityName AS DuplicateEntity,
    STRING_AGG(SubsystemName, ', ') AS Components,
    COUNT(*) AS [Count]
FROM CTE
GROUP BY EntityName
HAVING COUNT(*) > 1
ORDER BY EntityName;

-- Endpoints
SELECT 
    'api/' + LOWER(SubsystemName) + '/' + CollectionSlug AS ApiCollectionUrl, 
    COUNT(*) - 1 AS Duplicates
FROM metadata.TEntity
GROUP BY SubsystemName, CollectionSlug
ORDER BY COUNT(*) desc, SubsystemName, CollectionSlug;
