namespace Spindle;

/// <summary>
/// Represents an entity configuration for code generation with component, collection, and storage information
/// </summary>
[Serializable]
public class Entity
{
    private const string DefaultValue = "-";
    
    // Component properties
    public string ComponentType { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentPart { get; set; } = string.Empty;

    // Entity properties
    public string EntityName { get; set; } = string.Empty;

    // Collection properties
    public string CollectionSlug { get; set; } = string.Empty;
    public string CollectionKey { get; set; } = string.Empty;

    // Storage properties
    public string StorageStructure { get; set; } = string.Empty;
    public string StorageSchema { get; set; } = string.Empty;
    public string StorageTable { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string? StorageTableRename { get; set; }

    /// <summary>
    /// Gets the number of key components in the storage key based on comma separators.
    /// </summary>
    public int StorageKeySize => StorageKey?.Count(x => x == ',') + 1 ?? 0;

    /// <summary>
    /// Gets the storage structure prefix based on the storage structure type.
    /// </summary>
    /// <returns>A single character prefix representing the storage structure type.</returns>
    public string GetStorageStructurePrefix()
    {
        return StorageStructure switch
        {
            "Table" => "T",
            "View" => "V",
            "Procedure" => "P",
            "Projection" => "Q", // Consider "R" for "pRojection" or "J" for "proJection" if needed
            _ => "X"
        };
    }

    /// <summary>
    /// Constructs the collection path based on component type and configuration.
    /// </summary>
    /// <returns>The formatted collection path.</returns>
    public string GetCollectionPath()
    {
        if (string.IsNullOrWhiteSpace(ComponentName) || string.IsNullOrWhiteSpace(CollectionSlug))
            return string.Empty;

        var component = ComponentName.ToLowerInvariant();
        
        if (ComponentType.Equals("Plugin", StringComparison.OrdinalIgnoreCase))
        {
            return BuildPluginCollectionPath(component);
        }

        return $"{component}/{CollectionSlug}";
    }

    /// <summary>
    /// Generates the namespace string based on component and entity configuration.
    /// </summary>
    /// <returns>The formatted namespace string.</returns>
    public string GetNamespace()
    {
        var namespaceParts = new List<string> { ComponentName };

        if (!string.IsNullOrWhiteSpace(ComponentPart) && ComponentPart != DefaultValue)
        {
            namespaceParts.Add(ComponentPart);
        }

        if (!string.IsNullOrWhiteSpace(EntityName) && EntityName != DefaultValue)
        {
            namespaceParts.Add(EntityName);
        }

        return string.Join(".", namespaceParts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    /// <summary>
    /// Builds the collection path specifically for plugin components.
    /// </summary>
    /// <param name="component">The lowercase component name.</param>
    /// <returns>The formatted plugin collection path.</returns>
    private string BuildPluginCollectionPath(string component)
    {
        if (string.IsNullOrWhiteSpace(ComponentPart))
            return $"{component}/{CollectionSlug}";

        var part = ComponentPart.ToLowerInvariant();
        var slug = CollectionSlug;

        // Remove redundant part prefix from slug if present
        var expectedPrefix = $"{part}-";
        if (slug.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            slug = slug.Substring(expectedPrefix.Length);
        }

        return $"{component}/{part}/{slug}";
    }
}