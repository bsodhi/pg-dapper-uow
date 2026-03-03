// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Data;
using PostgresDapperUow.Abstractions;

namespace PostgresDapperUow.UnitOfWork;

/// <summary>
/// Default PostgreSQL implementation of <see cref="IUnitOfWork"/> using Dapper.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DapperUnitOfWork"/> manages a single database connection and
/// transaction lifecycle. The transaction is started upon construction and
/// remains active until explicitly committed or rolled back.
/// </para>
///
/// <para>
/// If the unit of work is disposed without being committed, the transaction
/// is rolled back automatically.
/// </para>
///
/// <para>
/// Instances are intended to be scoped (e.g., per HTTP request) and must not
/// be reused after completion.
/// </para>
/// </remarks>
public sealed class DapperUnitOfWork : IUnitOfWork
{
    private readonly IDbConnection _connection;
    private readonly IDbTransaction _transaction;
    private int _completed;
    internal bool IsCompleted => Volatile.Read(ref _completed) == 1;

    public IDbConnection Connection => _connection;
    public IDbTransaction Transaction => _transaction;

    /// <summary>
    /// Opens a new DB connection using the supplied factory and begins
    /// a new transaction.
    /// </summary>
    /// <param name="factory"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public DapperUnitOfWork(IDbConnectionFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _connection = factory.OpenConnection()
            ?? throw new InvalidOperationException("Failed to open database connection.");

        _transaction = _connection.BeginTransaction();
    }

    public void Commit()
    {
        EnsureNotCompleted();

        _transaction.Commit();
        Interlocked.Exchange(ref _completed, 1);
    }

    public void Rollback()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 1)
            return;

        _transaction.Rollback();
    }

    /// <summary>
    /// Underlying transaction is rolled back and disposed.
    /// DB connection is also disposed.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0)
        {
            try { _transaction.Rollback(); }
            catch { /* best effort */ }
        }

        _transaction.Dispose();
        _connection.Dispose();
    }

    private void EnsureNotCompleted()
    {
        if (Volatile.Read(ref _completed) == 1)
            throw new InvalidOperationException("UnitOfWork already completed.");
    }
}