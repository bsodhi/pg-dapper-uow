// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace PostgresDapperUow.Internal;

/// <summary>
/// Provides cached, compiled property accessors for runtime performance optimization.
/// </summary>
/// <remarks>
/// This factory replaces reflection-based <see cref="PropertyInfo.GetValue(object?)"/> calls
/// with compiled expression delegates. The compiled delegates are cached per
/// <see cref="PropertyInfo"/> instance to ensure that property access is fast,
/// thread-safe, and allocation-free after initial compilation.
///
/// This is an internal infrastructure component used by metadata and query
/// building layers to avoid repeated reflection during repository operations.
///
/// Delegates are compiled once per property and reused for the lifetime of the
/// application domain.
/// </remarks>
internal static class PropertyAccessorFactory
{
    private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object?>> GetterCache = new();

    public static Func<object, object?> CreateGetter(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return GetterCache.GetOrAdd(property, BuildGetter);
    }

    private static Func<object, object?> BuildGetter(PropertyInfo property)
    {
        var declaringType = property.DeclaringType
            ?? throw new InvalidOperationException(
                $"Property '{property.Name}' has no declaring type.");

        var param = Expression.Parameter(typeof(object), "instance");

        var cast = Expression.Convert(param, declaringType);
        var propertyAccess = Expression.Property(cast, property);
        var convert = Expression.Convert(propertyAccess, typeof(object));

        return Expression
            .Lambda<Func<object, object?>>(convert, param)
            .Compile();
    }
}