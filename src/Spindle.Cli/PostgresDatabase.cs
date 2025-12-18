using System.Data;

using Npgsql;

namespace Spindle;

public class PostgresDatabase : IDatabase
{
    private readonly GeneratorSettings _settings;

    public PostgresDatabase(GeneratorSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    private async Task<NpgsqlConnection> OpenDatabaseConnectionAsync()
    {
        var connection = new NpgsqlConnection(_settings.DatabaseConnection);
        await connection.OpenAsync();
        return connection;
    }

    private NpgsqlConnection OpenDatabaseConnection()
    {
        var connection = new NpgsqlConnection(_settings.DatabaseConnection);
        connection.Open();
        return connection;
    }

    public async Task<EntityList> GetEntitiesAsync(string? databaseObject = null)
    {
        const string baseQuery = "SELECT * FROM metadata.t_entity";
        const string orderBy = "ORDER BY component_name, component_feature, entity_name";

        var whereClause = string.IsNullOrEmpty(databaseObject)
            ? string.Empty
            : "WHERE storage_structure = $1";

        var query = $"{baseQuery} {whereClause} {orderBy}";

        using var connection = await OpenDatabaseConnectionAsync();
        using var command = new NpgsqlCommand(query, connection);

        if (!string.IsNullOrEmpty(databaseObject))
        {
            command.Parameters.AddWithValue(databaseObject);
        }

        using var reader = await command.ExecuteReaderAsync();
        var entities = new EntityList();

        while (await reader.ReadAsync())
        {
            var entity = CreateEntityFromReader(reader);
            entities.Add(entity);
        }

        return entities;
    }

    public EntityList GetEntities(string? databaseObject = null)
    {
        return GetEntitiesAsync(databaseObject).GetAwaiter().GetResult();
    }

    public async Task<DataTable> GetTableAsync(string schemaName, string tableName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
            throw new ArgumentException("Schema name cannot be null or empty", nameof(schemaName));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));

        using var connection = await OpenDatabaseConnectionAsync();
        var table = new DataTable(tableName);

        const string query = "SELECT * FROM \"{0}\".\"{1}\" WHERE 1=0";
        using var adapter = new NpgsqlDataAdapter(string.Format(query, schemaName, tableName), connection);

        adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
        adapter.Fill(table);

        return table;
    }

    public DataTable GetTable(string schemaName, string tableName)
    {
        return GetTableAsync(schemaName, tableName).GetAwaiter().GetResult();
    }

    public async Task<object?> GetScalarAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or empty", nameof(query));

        using var connection = await OpenDatabaseConnectionAsync();
        using var command = new NpgsqlCommand(query, connection);
        return await command.ExecuteScalarAsync();
    }

    public object? GetScalar(string query)
    {
        return GetScalarAsync(query).GetAwaiter().GetResult();
    }

    public async Task<string?> GetNativeTypeAsync(string tableName, string columnName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be null or empty", nameof(columnName));

        const string query = @"
            SELECT data_type 
            FROM information_schema.columns 
            WHERE table_name = $1 AND column_name = $2";

        using var connection = await OpenDatabaseConnectionAsync();
        using var command = new NpgsqlCommand(query, connection);

        command.Parameters.AddWithValue(tableName);
        command.Parameters.AddWithValue(columnName);

        return await command.ExecuteScalarAsync() as string;
    }

    public string? GetNativeType(string tableName, string columnName)
    {
        return GetNativeTypeAsync(tableName, columnName).GetAwaiter().GetResult();
    }

    public async Task<int?> GetColumnPrecisionAsync(string tableName, string columnName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be null or empty", nameof(columnName));

        const string query = @"
            SELECT numeric_precision 
            FROM information_schema.columns 
            WHERE table_name = $1 AND column_name = $2";

        using var connection = await OpenDatabaseConnectionAsync();
        using var command = new NpgsqlCommand(query, connection);

        command.Parameters.AddWithValue(tableName);
        command.Parameters.AddWithValue(columnName);

        var result = await command.ExecuteScalarAsync();
        return result switch
        {
            int precision => precision,
            long longPrecision => (int)longPrecision,
            null => null,
            _ => Convert.ToInt32(result)
        };
    }

    public int? GetColumnPrecision(string tableName, string columnName)
    {
        return GetColumnPrecisionAsync(tableName, columnName).GetAwaiter().GetResult();
    }

    public async Task<int?> GetColumnScaleAsync(string tableName, string columnName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be null or empty", nameof(columnName));

        const string query = @"
            SELECT numeric_scale 
            FROM information_schema.columns 
            WHERE table_name = $1 AND column_name = $2";

        using var connection = await OpenDatabaseConnectionAsync();
        using var command = new NpgsqlCommand(query, connection);

        command.Parameters.AddWithValue(tableName);
        command.Parameters.AddWithValue(columnName);

        var result = await command.ExecuteScalarAsync();
        return result switch
        {
            int scale => scale,
            long longScale => (int)longScale,
            null => null,
            _ => Convert.ToInt32(result)
        };
    }

    public int? GetColumnScale(string tableName, string columnName)
    {
        return GetColumnScaleAsync(tableName, columnName).GetAwaiter().GetResult();
    }

    public static string TypeNameOrAlias(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => "boolean",
            TypeCode.Byte => "smallint",
            TypeCode.SByte => "smallint",
            TypeCode.Int16 => "smallint",
            TypeCode.UInt16 => "integer",
            TypeCode.Int32 => "integer",
            TypeCode.UInt32 => "bigint",
            TypeCode.Int64 => "bigint",
            TypeCode.UInt64 => "numeric(20,0)",
            TypeCode.Single => "real",
            TypeCode.Double => "double precision",
            TypeCode.Decimal => "numeric",
            TypeCode.DateTime => "timestamp",
            TypeCode.String => "text",
            TypeCode.Char => "char(1)",
            _ => HandleSpecialTypes(type)
        };
    }

    private static string HandleSpecialTypes(Type type)
    {
        if (type == typeof(Guid)) return "uuid";
        if (type == typeof(byte[])) return "bytea";
        if (type == typeof(DateTimeOffset)) return "timestamptz";
        if (type == typeof(TimeSpan)) return "interval";
        if (type.IsArray && type.GetElementType() != null) return "jsonb";
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return TypeNameOrAlias(type.GetGenericArguments()[0]);
        }

        return "jsonb"; // Default for complex types
    }

    private static Entity CreateEntityFromReader(NpgsqlDataReader reader)
    {
        return new Entity
        {
            StorageStructure = reader["storage_structure"] as string ?? string.Empty,
            StorageSchema = reader["storage_schema"] as string ?? string.Empty,
            StorageTable = reader["storage_table"] as string ?? string.Empty,
            StorageKey = reader["storage_key"] as string ?? string.Empty,
            StorageTableRename = reader["storage_table_rename"] as string,
            ComponentType = reader["component_type"] as string ?? string.Empty,
            ComponentName = reader["component_name"] as string ?? string.Empty,
            ComponentPart = reader["component_part"] as string ?? string.Empty,
            EntityName = reader["entity_name"] as string ?? string.Empty,
            CollectionSlug = reader["collection_slug"] as string ?? string.Empty,
            CollectionKey = reader["collection_key"] as string ?? string.Empty
        };
    }

    // Extension method for PostgreSQL-specific functionality
    public async Task<bool> TableExistsAsync(string schemaName, string tableName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
            throw new ArgumentException("Schema name cannot be null or empty", nameof(schemaName));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));

        const string query = @"
            SELECT EXISTS (
                SELECT 1 
                FROM information_schema.tables 
                WHERE table_schema = $1 AND table_name = $2
            )";

        using var connection = await OpenDatabaseConnectionAsync();
        using var command = new NpgsqlCommand(query, connection);

        command.Parameters.AddWithValue(schemaName);
        command.Parameters.AddWithValue(tableName);

        var result = await command.ExecuteScalarAsync();
        return result is bool exists && exists;
    }

    public bool TableExists(string schemaName, string tableName)
    {
        return TableExistsAsync(schemaName, tableName).GetAwaiter().GetResult();
    }
}