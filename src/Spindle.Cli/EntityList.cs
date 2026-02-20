namespace Spindle;

/// <summary>
/// Represents a collection of entities with specialized query methods
/// </summary>
[Serializable]
public class EntityList : List<Entity>
{
    // Cache for frequently accessed data
    private readonly Dictionary<string, HashSet<string>> _componentCache = new();
    private readonly Dictionary<(string, string), HashSet<string>> _subcomponentCache = new();
    
    /// <summary>
    /// Gets all unique component names in the collection, ordered alphabetically
    /// </summary>
    public string[] GetComponents()
    {
        return this.Select(x => x.SubsystemName)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
    }

    /// <summary>
    /// Gets all unique subcomponents for a specific component, ordered alphabetically
    /// </summary>
    /// <param name="component">The component name to filter by.</param>
    public string[] GetSubcomponents(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
            return Array.Empty<string>();

        return this.Where(x => x.SubsystemName == component)
            .Select(x => x.SubsystemComponent)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
    }

    /// <summary>
    /// Gets all entity names for a specific component and feature combination
    /// </summary>
    /// <param name="component">The component name to filter by.</param>
    /// <param name="feature">The feature name to filter by.</param>
    public string[] GetEntities(string component, string feature)
    {
        if (string.IsNullOrWhiteSpace(component) || string.IsNullOrWhiteSpace(feature))
            return Array.Empty<string>();

        return this.Where(x => x.SubsystemName == component && x.SubsystemComponent == feature)
            .Select(x => x.EntityName)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
    }

    /// <summary>
    /// Generates a list of schema migration instructions for a specific component and feature
    /// </summary>
    /// <param name="component">The component name to analyze.</param>
    /// <param name="feature">The feature name to analyze.</param>
    public string[] GetFutureSchemaChanges(string component, string feature)
    {
        if (string.IsNullOrWhiteSpace(component) || string.IsNullOrWhiteSpace(feature))
            return Array.Empty<string>();

        var features = this.Where(x => x.SubsystemName == component && x.SubsystemComponent == feature)
            .OrderBy(x => x.StorageTable)
            .ThenBy(x => x.StorageSchema);

        var changes = new List<string>();
        var futureSchemaName = component.ToLower();

        foreach (var item in features)
        {
            // Check for schema migration
            if (!string.Equals(item.StorageSchema, futureSchemaName, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add($"* Move table `{item.StorageTable}` from schema `{item.StorageSchema}` to schema `{futureSchemaName}`.");
            }

            // Check for table rename
            if (!string.IsNullOrWhiteSpace(item.StorageTableRename) && 
                !string.Equals(item.StorageTable, item.StorageTableRename, StringComparison.Ordinal))
            {
                changes.Add($"* Rename table from `{item.StorageTable}` to `{item.StorageTableRename}`.");
            }
        }

        return changes.Distinct().ToArray();
    }

    /// <summary>
    /// Gets a single entity matching the specified criteria
    /// </summary>
    /// <param name="component">The component name to match.</param>
    /// <param name="feature">The feature name to match.</param>
    /// <param name="entity">The entity name to match.</param>
    /// <returns>The matching entity</returns>
    /// <exception cref="ArgumentException">Thrown when any parameter is null or whitespace.</exception>
    /// <exception cref="EntityNotFoundException">Thrown when no matching entity is found.</exception>
    /// <exception cref="MultipleEntitiesException">Thrown when multiple matching entities are found.</exception>
    public Entity GetEntity(string component, string feature, string entity)
    {
        if (string.IsNullOrWhiteSpace(component))
            throw new ArgumentException("Component name cannot be null or empty.", nameof(component));
        if (string.IsNullOrWhiteSpace(feature))
            throw new ArgumentException("Feature name cannot be null or empty.", nameof(feature));
        if (string.IsNullOrWhiteSpace(entity))
            throw new ArgumentException("Entity name cannot be null or empty.", nameof(entity));

        var entities = this.Where(x => 
            x.SubsystemName == component && 
            x.SubsystemComponent == feature && 
            x.EntityName == entity)
            .ToList();

        return entities.Count switch
        {
            0 => throw new EntityNotFoundException(component, feature, entity),
            1 => entities[0],
            _ => throw new MultipleEntitiesException(component, feature, entity, entities.Count)
        };
    }

    /// <summary>
    /// Tries to get a single entity matching the specified criteria
    /// </summary>
    /// <param name="component">The component name to match.</param>
    /// <param name="feature">The feature name to match.</param>
    /// <param name="entity">The entity name to match.</param>
    /// <param name="result">The matching entity, if found.</param>
    /// <returns>True if exactly one matching entity was found; otherwise, false</returns>
    public bool TryGetEntity(string component, string feature, string entity, out Entity? result)
    {
        result = null;
        
        if (string.IsNullOrWhiteSpace(component) || 
            string.IsNullOrWhiteSpace(feature) || 
            string.IsNullOrWhiteSpace(entity))
        {
            return false;
        }

        var entities = this.Where(x => 
            x.SubsystemName == component && 
            x.SubsystemComponent == feature && 
            x.EntityName == entity)
            .ToList();

        if (entities.Count == 1)
        {
            result = entities[0];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears the collection and any associated caches
    /// </summary>
    public new void Clear()
    {
        base.Clear();
        _componentCache.Clear();
        _subcomponentCache.Clear();
    }
}

/// <summary>
/// Exception thrown when an entity is not found
/// </summary>
public class EntityNotFoundException : Exception
{
    public string Component { get; }
    public string Feature { get; }
    public string Entity { get; }

    public EntityNotFoundException(string component, string feature, string entity)
        : base($"No entity found: entity '{entity}'; feature '{feature}'; component '{component}'.")
    {
        Component = component;
        Feature = feature;
        Entity = entity;
    }
}

/// <summary>
/// Exception thrown when multiple entities are found when expecting only one
/// </summary>
public class MultipleEntitiesException : Exception
{
    public string Component { get; }
    public string Feature { get; }
    public string Entity { get; }
    public int Count { get; }

    public MultipleEntitiesException(string component, string feature, string entity, int count)
        : base($"Multiple entities found ({count}): entity '{entity}'; feature '{feature}'; component '{component}'.")
    {
        Component = component;
        Feature = feature;
        Entity = entity;
        Count = count;
    }
}