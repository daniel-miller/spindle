using System.Data;

namespace CodeGenerator;

/// <summary>
/// Compares DataColumns for consistent ordering based on data type priority and column name
/// </summary>
public class EntityColumnComparer : IComparer<DataColumn>
{
    private static readonly IReadOnlyDictionary<Type, string> TypeAliases = new Dictionary<Type, string>
    {
        { typeof(bool), "bool" },
        { typeof(byte), "byte" },
        { typeof(byte[]), "byte[]" },
        { typeof(char), "char" },
        { typeof(DateTime), "DateTime" },
        { typeof(DateTimeOffset), "DateTimeOffset" },
        { typeof(decimal), "decimal" },
        { typeof(double), "double" },
        { typeof(float), "float" },
        { typeof(Guid), "Guid" },
        { typeof(int), "int" },
        { typeof(long), "long" },
        { typeof(object), "object" },
        { typeof(sbyte), "sbyte" },
        { typeof(short), "short" },
        { typeof(string), "string" },
        { typeof(uint), "uint" },
        { typeof(ulong), "ulong" },
        { typeof(ushort), "ushort" }
    };

    private static readonly IReadOnlyDictionary<string, int> DataTypePriority = new Dictionary<string, int>
    {
        ["Guid"] = 0,
        ["Boolean"] = 1,
        ["String"] = 2,
        ["Int32"] = 3,
        ["Int64"] = 4,
        ["Decimal"] = 5,
        ["Double"] = 6,
        ["DateTimeOffset"] = 7,
        ["DateTime"] = 8,
        ["Byte[]"] = 9
    };

    /// <summary>
    /// Gets the C# type alias for the specified type, or throws if the type is not supported
    /// </summary>
    /// <param name="type">The type to get the alias for.</param>
    /// <returns>The C# type alias string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when the type is not supported.</exception>
    public static string GetTypeAlias(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!TypeAliases.TryGetValue(type, out string? alias))
        {
            throw new NotSupportedException($"Type '{type.FullName}' is not supported.");
        }

        return alias;
    }

    /// <summary>
    /// Compares two DataColumns based on their data type priority and column name
    /// </summary>
    /// <param name="x">The first DataColumn to compare.</param>
    /// <param name="y">The second DataColumn to compare.</param>
    /// <returns>
    /// Less than zero if x is less than y.
    /// Zero if x equals y.
    /// Greater than zero if x is greater than y.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when either column is null.</exception>
    public int Compare(DataColumn? x, DataColumn? y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        // First compare by data type priority
        int xPriority = GetDataTypePriority(x.DataType.Name);
        int yPriority = GetDataTypePriority(y.DataType.Name);

        int typeComparison = xPriority.CompareTo(yPriority);
        if (typeComparison != 0)
        {
            return typeComparison;
        }

        // If types have same priority, compare by column name
        return string.Compare(x.ColumnName, y.ColumnName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the priority index for the specified data type name
    /// </summary>
    /// <param name="typeName">The data type name.</param>
    /// <returns>The priority index for sorting.</returns>
    /// <exception cref="ArgumentException">Thrown when the data type name is not recognized.</exception>
    private static int GetDataTypePriority(string typeName)
    {
        if (DataTypePriority.TryGetValue(typeName, out int priority))
        {
            return priority;
        }

        throw new ArgumentException($"Data type '{typeName}' is not recognized.", nameof(typeName));
    }
}