// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

namespace PostgresDapperUow.Attributes;

/// <summary>
/// Specifies the database table name associated with an entity type.
/// </summary>
/// <remarks>
/// <para>
/// When applied to an entity class, <see cref="TableAttribute"/> overrides
/// the default table naming convention used by the repository.
/// </para>
///
/// <para>
/// If this attribute is not present, the table name is derived from the
/// entity type name using the configured naming convention.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class TableAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
