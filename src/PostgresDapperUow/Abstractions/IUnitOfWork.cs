// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Data;

namespace PostgresDapperUow.Abstractions;
/// <summary>
/// Represents an explicit database transaction boundary.
/// </summary>
/// <remarks>
/// An <see cref="IUnitOfWork"/> encapsulates a single database transaction
/// and provides access to the underlying <see cref="IDbConnection"/> and
/// <see cref="IDbTransaction"/> used by repositories.
///
/// <para>
/// A unit of work:
/// </para>
/// <list type="bullet">
/// <item>Begins a transaction upon creation</item>
/// <item>Commits changes explicitly via <see cref="Commit"/></item>
/// <item>Rolls back automatically if not committed</item>
/// </list>
///
/// <para>
/// Once <see cref="Commit"/> or <see cref="Rollback"/> is called, the unit of
/// work is considered completed and must not be reused.
/// </para>
/// </remarks>
public interface IUnitOfWork : IDisposable
{
    IDbConnection Connection { get; }
    IDbTransaction Transaction { get; }

    void Commit();
    void Rollback();
}