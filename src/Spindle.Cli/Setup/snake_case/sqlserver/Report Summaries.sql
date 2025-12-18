-- Components
select distinct component_name as Toolkit from metadata.t_entity order by component_name;

-- Features
with cte as (select distinct component_feature, component_name from metadata.t_entity)
select component_feature as Feature, string_agg(component_name, ', ') as Components from cte group by component_feature order by component_feature;

-- Entities
with cte as (select distinct entity_name, component_name from metadata.t_entity)
select entity_name as Entity, string_agg(component_name, ', ') as Components from cte group by entity_name order by entity_name;

-- Duplicate Entities
with cte as (select distinct entity_name, component_feature, component_name from metadata.t_entity)
select entity_name as DuplicateEntity, string_agg(component_name, ', ') as Components, count(*) as [Count] from cte group by entity_name having count(*) > 1 order by entity_name;

-- Endpoints
select 'api/' + LOWER(component_name) + '/' + collection_slug as ApiCollectionUrl, COUNT(*) - 1 AS Duplicates from metadata.t_entity GROUP BY component_name, collection_slug ORDER by component_name, collection_slug;