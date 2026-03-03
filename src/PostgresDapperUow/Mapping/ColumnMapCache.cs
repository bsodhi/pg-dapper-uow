// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Reflection;
using PostgresDapperUow.Attributes;
using PostgresDapperUow.Internal;

namespace PostgresDapperUow.Mapping;

/// <summary>
/// Provides cached column mapping metadata for entity types.
/// </summary>
/// <remarks>
/// This cache builds <see cref="ColumnMap"/> metadata once per entity type
/// and reuses it for the lifetime of the application domain.
///
/// It eliminates repeated reflection by:
/// <list type="bullet">
/// <item>Inspecting entity properties once</item>
/// <item>Resolving mapping attributes</item>
/// <item>Compiling property accessors</item>
/// </list>
///
/// The cache is thread-safe and guarantees that metadata construction
/// occurs only once per type.
///
/// This component is internal infrastructure and should not be used
/// directly by application code.
/// </remarks>
internal static class ColumnMapCache
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<ColumnMap>> Cache = new();

    public static IReadOnlyList<ColumnMap> For<T>()
        => Cache.GetOrAdd(typeof(T), Build);

    private static IReadOnlyList<ColumnMap> Build(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(p =>
            {
                var attr = p.GetCustomAttribute<ColumnAttribute>();

                return new ColumnMap
                {
                    Property = p,
                    ColumnName = attr?.Name ?? Naming.ToSnakeCase(p.Name),
                    DbType = attr?.DbType,
                    Insertable = attr?.Insertable ?? true,
                    Updatable = attr?.Updatable ?? true,
                    IsJson = attr is JsonColumnAttribute,
                    Getter = PropertyAccessorFactory.CreateGetter(p)
                };
            })
            .ToList();
    }
}