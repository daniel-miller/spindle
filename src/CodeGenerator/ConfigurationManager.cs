using System.Collections.Concurrent;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace CodeGenerator;

/// <summary>
/// Manages application configuration with support for multiple sources, caching, and environment-specific settings
/// </summary>
public sealed class ConfigurationManager
{
    private static readonly Lazy<ConfigurationManager> Instance = new(() => new ConfigurationManager());
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly object _configLock = new();
    private IConfigurationRoot? _configuration;
    private IChangeToken? _reloadToken;

    /// <summary>
    /// Gets the singleton instance of the ConfigurationManager
    /// </summary>
    public static ConfigurationManager Current => Instance.Value;

    private ConfigurationManager() { }

    /// <summary>
    /// Gets the current configuration root, building it if necessary
    /// </summary>
    public IConfigurationRoot Configuration
    {
        get
        {
            if (_configuration == null)
            {
                lock (_configLock)
                {
                    _configuration ??= BuildConfiguration();
                    _reloadToken = _configuration.GetReloadToken();
                    _reloadToken.RegisterChangeCallback(_ => OnConfigurationChanged(), null);
                }
            }
            return _configuration;
        }
    }

    /// <summary>
    /// Gets strongly-typed settings from the specified configuration section
    /// </summary>
    /// <typeparam name="T">The type to bind the settings to.</typeparam>
    /// <param name="sectionName">The name of the configuration section.</param>
    /// <returns>The settings object</returns>
    /// <exception cref="ArgumentNullException">Thrown when sectionName is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the section doesn't exist or can't be bound.</exception>
    public T GetSettings<T>(string sectionName) where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(sectionName))
            throw new ArgumentNullException(nameof(sectionName));

        var cacheKey = $"{typeof(T).FullName}:{sectionName}";
        
        if (_cache.TryGetValue(cacheKey, out var cached))
            return (T)cached;

        var section = Configuration.GetSection(sectionName);
        if (!section.Exists())
            throw new InvalidOperationException($"Configuration section '{sectionName}' not found.");

        var settings = section.Get<T>(options => options.BindNonPublicProperties = true);
        if (settings == null)
        {
            throw new InvalidOperationException(
                $"Unable to bind configuration section '{sectionName}' to type '{typeof(T).Name}'. " +
                "Ensure the section exists and matches the target type structure.");
        }

        _cache.TryAdd(cacheKey, settings);
        return settings;
    }

    /// <summary>
    /// Tries to get strongly-typed settings from the specified configuration section
    /// </summary>
    /// <typeparam name="T">The type to bind the settings to.</typeparam>
    /// <param name="sectionName">The name of the configuration section.</param>
    /// <param name="settings">The settings object, if found.</param>
    /// <returns>True if the settings were successfully retrieved; otherwise, false</returns>
    public bool TryGetSettings<T>(string sectionName, out T? settings) where T : class, new()
    {
        settings = null;
        
        try
        {
            settings = GetSettings<T>(sectionName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a single configuration value
    /// </summary>
    /// <typeparam name="T">The type to convert the value to.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value</returns>
    public T? GetValue<T>(string key)
    {
        return Configuration.GetValue<T>(key);
    }

    /// <summary>
    /// Gets a single configuration value with a default fallback
    /// </summary>
    /// <typeparam name="T">The type to convert the value to.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The configuration value or default</returns>
    public T GetValue<T>(string key, T defaultValue)
    {
        return Configuration.GetValue<T>(key) ?? defaultValue;
    }

    /// <summary>
    /// Gets the connection string with the specified name
    /// </summary>
    /// <param name="name">The name of the connection string.</param>
    /// <returns>The connection string value</returns>
    /// <exception cref="InvalidOperationException">Thrown when the connection string is not found.</exception>
    public string GetConnectionString(string name)
    {
        var connectionString = Configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Connection string '{name}' not found.");
        
        return connectionString;
    }

    /// <summary>
    /// Clears the settings cache
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Forces a reload of the configuration
    /// </summary>
    public void Reload()
    {
        lock (_configLock)
        {
            _cache.Clear();
            _configuration = BuildConfiguration();
            _reloadToken = _configuration.GetReloadToken();
            _reloadToken.RegisterChangeCallback(_ => OnConfigurationChanged(), null);
        }
    }

    /// <summary>
    /// Builds the configuration from various sources
    /// </summary>
    private static IConfigurationRoot BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                         ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                         ?? "Production";

        var basePath = AppContext.BaseDirectory;
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath);

        // Add configuration sources in order of precedence (lowest to highest)
        
        // 1. Base configuration
        AddJsonFile(builder, basePath, "appsettings.json", optional: false);
        
        // 2. Environment-specific configuration
        AddJsonFile(builder, basePath, $"appsettings.{environment}.json", optional: true);
        
        // 3. Local developer overrides (not committed to source control)
        AddJsonFile(builder, basePath, "appsettings.local.json", optional: true);

        // 4. Environment variables
        // builder.AddEnvironmentVariables();

        // 5. Command line arguments if available
        /*
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            builder.AddCommandLine(args.Skip(1).ToArray());
        }
        */

        return builder.Build();
    }

    /// <summary>
    /// Adds a JSON file to the configuration builder with error handling
    /// </summary>
    private static void AddJsonFile(IConfigurationBuilder builder, string basePath, string filename, bool optional)
    {
        var filePath = Path.Combine(basePath, filename);
        
        if (!optional && !File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Required configuration file '{filename}' not found at '{filePath}'.");
        }

        builder.AddJsonFile(filename, optional: optional, reloadOnChange: true);
    }

    /// <summary>
    /// Handles configuration change events
    /// </summary>
    private void OnConfigurationChanged()
    {
        _cache.Clear();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Occurs when the configuration is reloaded
    /// </summary>
    public event EventHandler? ConfigurationChanged;
}

/// <summary>
/// Provides static helper methods for quick configuration access
/// </summary>
public static class Config
{
    /// <summary>
    /// Gets strongly-typed settings from the specified configuration section
    /// </summary>
    /// <typeparam name="T">The type to bind the settings to.</typeparam>
    /// <param name="sectionName">The name of the configuration section.</param>
    /// <returns>The settings object</returns>
    public static T GetSettings<T>(string sectionName) where T : class, new()
        => ConfigurationManager.Current.GetSettings<T>(sectionName);

    /// <summary>
    /// Gets a configuration value
    /// </summary>
    /// <typeparam name="T">The type to convert the value to.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value</returns>
    public static T? GetValue<T>(string key)
        => ConfigurationManager.Current.GetValue<T>(key);

    /// <summary>
    /// Gets a configuration value with a default fallback
    /// </summary>
    /// <typeparam name="T">The type to convert the value to.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The configuration value or default</returns>
    public static T GetValue<T>(string key, T defaultValue)
        => ConfigurationManager.Current.GetValue<T>(key, defaultValue);

    /// <summary>
    /// Gets the connection string with the specified name
    /// </summary>
    /// <param name="name">The name of the connection string.</param>
    /// <returns>The connection string value</returns>
    public static string GetConnectionString(string name)
        => ConfigurationManager.Current.GetConnectionString(name);
}