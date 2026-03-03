// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using PostgresDapperUow.Abstractions;
using PostgresDapperUow.Attributes;

namespace PostgresDapperUow.Tests.Integration;

[Table("users")]
public sealed class TestUser : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;

    [JsonColumn("metadata", DbType = "jsonb")]
    public string? Metadata { get; set; }
}