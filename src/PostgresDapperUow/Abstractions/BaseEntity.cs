// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

namespace PostgresDapperUow.Abstractions;

/// <summary>
/// All models/entities that need to participate in the persistence operations
/// via the <see cref="IRepository"/> must inherit from this class.
/// </summary>
public abstract class BaseEntity
{
    public long Id { get; set; }
    public int TxnNo { get; set; }
    public DateTime InsertedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}