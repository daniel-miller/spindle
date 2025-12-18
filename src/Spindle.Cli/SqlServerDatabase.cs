using System.Data;

using Microsoft.Data.SqlClient;

namespace Spindle;

public class SqlServerDatabase : IDatabase
{
    private readonly GeneratorSettings _settings;

    public SqlServerDatabase(GeneratorSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    private async Task<SqlConnection> OpenDatabaseConnectionAsync()
    {
        var connection = new SqlConnection(_settings.DatabaseConnection);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<EntityList> GetEntitiesAsync(string? storageStructure = null)
    {
        const string baseQuery = "SELECT * FROM metadata.TEntity";
        const string orderBy = "ORDER BY ComponentName, ComponentPart, EntityName";

        var whereClause = string.IsNullOrEmpty(storageStructure)
            ? string.Empty
            : "WHERE StorageStructure LIKE @StorageStructure";

        var query = $"{baseQuery} {whereClause} {orderBy}";

        using var connection = await OpenDatabaseConnectionAsync();
        using var command = new SqlCommand(query, connection);

        if (!string.IsNullOrEmpty(storageStructure))
        {
            command.Parameters.AddWithValue("@StorageStructure", $"%{storageStructure}%");
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

    public EntityList GetEntities(string? storageStructure = null)
    {
        return GetEntitiesAsync(storageStructure).GetAwaiter().GetResult();
    }

    public async Task<DataTable> GetTableAsync(string schemaName, string tableName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
            throw new ArgumentException("Schema name cannot be null or empty", nameof(schemaName));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));

        using var connection = await OpenDatabaseConnectionAsync();
        var table = new DataTable(tableName);

        var query = "SELECT * FROM [{0}].[{1}] WHERE 1=0";
        using var adapter = new SqlDataAdapter(string.Format(query, schemaName, tableName), connection);

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
        using var command = new SqlCommand(query, connection);
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
            SELECT DATA_TYPE 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName";

        using var connection = await OpenDatabaseConnectionAsync();
        using var command = new SqlCommand(query, connection);

        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@ColumnName", columnName);

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
            SELECT CAST(NUMERIC_PRECISION AS INT) 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName";

        using var connection = await OpenDatabaseConnectionAsync();
        using var command = new SqlCommand(query, connection);

        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@ColumnName", columnName);

        var result = await command.ExecuteScalarAsync();
        return result as int?;
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
            SELECT NUMERIC_SCALE 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName";

        using var connection = await OpenDatabaseConnectionAsync();
        using var command = new SqlCommand(query, connection);

        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@ColumnName", columnName);

        var result = await command.ExecuteScalarAsync();
        return result as int?;
    }

    public int? GetColumnScale(string tableName, string columnName)
    {
        return GetColumnScaleAsync(tableName, columnName).GetAwaiter().GetResult();
    }

    private static Entity CreateEntityFromReader(SqlDataReader reader)
    {
        return new Entity
        {
            StorageStructure = reader["StorageStructure"] as string ?? string.Empty,
            StorageSchema = reader["StorageSchema"] as string ?? string.Empty,
            StorageTable = reader["StorageTable"] as string ?? string.Empty,
            StorageKey = reader["StorageKey"] as string ?? string.Empty,
            StorageTableRename = reader["StorageTableRename"] as string,
            ComponentType = reader["ComponentType"] as string ?? string.Empty,
            ComponentName = reader["ComponentName"] as string ?? string.Empty,
            ComponentPart = reader["ComponentPart"] as string ?? string.Empty,
            EntityName = reader["EntityName"] as string ?? string.Empty,
            CollectionSlug = reader["CollectionSlug"] as string ?? string.Empty,
            CollectionKey = reader["CollectionKey"] as string ?? string.Empty
        };
    }

    public class ConfigurationColumnComparer : IComparer<DataColumn>
    {
        public int Compare(DataColumn? x, DataColumn? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            return string.Compare(x.ColumnName, y.ColumnName, StringComparison.Ordinal);
        }
    }
}