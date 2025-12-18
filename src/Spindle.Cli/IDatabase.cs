using System.Data;

namespace Spindle;

public interface IDatabase
{
    int? GetColumnPrecision(string table, string column);
    
    int? GetColumnScale(string table, string column);

    EntityList GetEntities(string? databaseObject = null);

    string? GetNativeType(string table, string column);

    object? GetScalar(string select);

    DataTable GetTable(string schemaName, string tableName);
}