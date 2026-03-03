// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Reflection;

namespace PostgresDapperUow.Mapping;

/// <summary>
/// Represents metadata describing the mapping between an entity property
/// and a database column.
/// </summary>
/// <remarks>
/// A <see cref="ColumnMap"/> instance contains all information required by the
/// repository layer to generate SQL statements and bind parameters without
/// using runtime reflection.
///
/// This includes:
/// <list type="bullet">
/// <item>The CLR property being mapped</item>
/// <item>The database column name</item>
/// <item>Insert/update participation flags</item>
/// <item>Optional PostgreSQL type casting</item>
/// <item>Compiled property accessor delegate</item>
/// </list>
///
/// Instances are created and cached by <see cref="ColumnMapCache"/> and are
/// intended for internal infrastructure use only.
/// </remarks>
internal sealed class ColumnMap
{
    public required PropertyInfo Property { get; init; }
    public required string ColumnName { get; init; }
    public string? DbType { get; init; }
    public bool Insertable { get; init; }
    public bool Updatable { get; init; }
    public bool IsJson { get; init; }
    public required Func<object, object?> Getter { get; init; }

    public string SqlParameter =>
        DbType is null
            ? "@" + Property.Name
            : $"@{Property.Name}::{DbType}";
}