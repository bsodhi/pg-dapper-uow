// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

namespace PostgresDapperUow.Attributes;

/// <summary>
/// Indicates that a property maps to a PostgreSQL JSON/JSONB column.
/// </summary>
/// <remarks>
/// Properties marked with this attribute are treated as JSON columns by
/// the repository layer. The property value is expected to be stored as
/// a JSON string in the database.
/// </remarks>
public sealed class JsonColumnAttribute : ColumnAttribute
{
    public JsonColumnAttribute(string name)
        : base(name)
    {
        DbType = "jsonb";
    }
}
