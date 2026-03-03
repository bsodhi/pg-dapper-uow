// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Concurrent;
using Dapper;
using PostgresDapperUow.Mapping;

namespace PostgresDapperUow.Internal;

/// <summary>
/// Builds SQL WHERE clauses and corresponding Dapper parameters from a filter object.
/// </summary>
/// <remarks>
/// This component converts an anonymous or strongly-typed filter object into a
/// parameterized SQL WHERE clause using the entity's column mapping metadata.
///
/// Supported behaviors:
/// <list type="bullet">
/// <item>Equality comparison (<c>=</c>)</item>
/// <item>NULL comparison (<c>IS NULL</c>)</item>
/// <item>Collection filtering using PostgreSQL <c>= ANY(...)</c></item>
/// <item>Enum value handling</item>
/// <item>DateOnly conversion</item>
/// </list>
///
/// Column names are resolved through <see cref="ColumnMapCache"/> to ensure that
/// only mapped properties are allowed, preventing accidental SQL injection via
/// property names.
///
/// This class is internal infrastructure and is not intended to be used directly
/// by consumers. It is invoked by <see cref="DapperRepository{T}"/> during query
/// execution.
/// </remarks>
internal static class WhereObjectBuilder
{
    private static readonly ConcurrentDictionary<Type, FilterMetadata> FilterCache = new();

    public static (string Sql, DynamicParameters Params) Build<T>(object? filters)
    {
        if (filters is null)
            return ("", new DynamicParameters());

        var filterType = filters.GetType();
        var metadata = FilterCache.GetOrAdd(filterType, BuildFilterMetadata);

        var columnMap = ColumnMapCache.For<T>()
            .ToDictionary(c => c.Property.Name, c => c);

        var clauses = new List<string>(metadata.Properties.Count);
        var parameters = new DynamicParameters();

        foreach (var prop in metadata.Properties)
        {
            if (!columnMap.TryGetValue(prop.Name, out var column))
                throw new InvalidOperationException(
                    $"No mapped column for property '{prop.Name}' on '{typeof(T).Name}'.");

            var value = prop.Getter(filters);
            var columnName = Quote(column.ColumnName);

            if (value is null)
            {
                clauses.Add($"{columnName} IS NULL");
                continue;
            }

            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (underlying.IsEnum)
                value = value.ToString();

            if (value is DateOnly d)
                value = d.ToDateTime(TimeOnly.MinValue);

            if (value is IEnumerable enumerable && value is not string)
            {
                var list = enumerable.Cast<object?>().ToArray();

                if (list.Length == 0)
                    throw new InvalidOperationException(
                        $"Empty collection provided for filter '{prop.Name}'.");

                clauses.Add($"{columnName} = ANY(@{column.SqlParameter})");
                parameters.Add(prop.Name, list);
                continue;
            }

            clauses.Add($"{columnName} = {column.SqlParameter}");
            parameters.Add(prop.Name, value);
        }

        return clauses.Count == 0
            ? ("", parameters)
            : ("WHERE " + string.Join(" AND ", clauses), parameters);
    }

    private static FilterMetadata BuildFilterMetadata(Type type)
    {
        var properties = type.GetProperties()
            .Where(p => p.CanRead)
            .Select(p => new FilterProperty
            {
                Name = p.Name,
                PropertyType = p.PropertyType,
                Getter = PropertyAccessorFactory.CreateGetter(p)
            })
            .ToList();

        return new FilterMetadata(properties);
    }

    private static string Quote(string identifier)
        => $"\"{identifier}\"";

    private sealed record FilterMetadata(IReadOnlyList<FilterProperty> Properties);

    private sealed class FilterProperty
    {
        public required string Name { get; init; }
        public required Type PropertyType { get; init; }
        public required Func<object, object?> Getter { get; init; }
    }
}