// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

namespace PostgresDapperUow.Attributes;

/// <summary>
/// Specifies database column mapping details for an entity property.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ColumnAttribute"/> allows customization of how a CLR property
/// maps to a database column.
/// </para>
///
/// <para>
/// Supported configuration options include:
/// </para>
/// <list type="bullet">
/// <item>Explicit column name override</item>
/// <item>Optional PostgreSQL type casting</item>
/// <item>Control over insert and update participation</item>
/// </list>
///
/// <para>
/// If this attribute is not present, the column name is derived using the
/// configured naming convention.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public string? DbType { get; init; }

    public bool Insertable { get; init; } = true;
    public bool Updatable { get; init; } = true;
}
