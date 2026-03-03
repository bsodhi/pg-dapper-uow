// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

namespace PostgresDapperUow.Abstractions;

/// <summary>
/// Defines a transactional repository abstraction for an entity type.
/// </summary>
/// <typeparam name="T">
/// The entity type managed by the repository.
/// </typeparam>
/// <remarks>
/// <para>
/// A repository provides basic data access operations for a mapped entity
/// and operates within the context of an <see cref="IUnitOfWork"/>.
/// </para>
///
/// <para>
/// Implementations are expected to:
/// </para>
/// <list type="bullet">
/// <item>Use parameterized SQL queries</item>
/// <item>Participate in the current transaction</item>
/// <item>Enforce optimistic concurrency where applicable</item>
/// </list>
///
/// <para>
/// Repository instances must not outlive the associated unit of work.
/// </para>
/// </remarks>

public interface IRepository<T>
{
    /// <summary>
    /// Fetches an entity by its PK.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<T?> GetById(long id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new record in the DB for supplied instance of an entity.
    /// <seealso cref="Attributes.ColumnAttribute"/>
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="ct"></param>
    /// <returns>PK of the new record.</returns>
    Task<long> Create(T entity, CancellationToken ct = default);

    /// <summary>
    /// Updates the given entity in the DB. All properties that are marked
    /// updatable will be affected in the DB row.
    /// <seealso cref="Attributes.ColumnAttribute"/>
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<bool> Update(T entity, CancellationToken ct = default);

    /// <summary>
    /// Hard deletes an entity from the DB.
    /// </summary>
    /// <param name="id">PK of the row to be deleted.</param>
    /// <param name="ct"></param>
    /// <returns>true if the delete was successful.</returns>
    Task<bool> DeleteById(long id, CancellationToken ct = default);

    /// <summary>
    /// Finds the matching row for the given entity based on the supplied
    /// criteria. Only one or zero row is expected.
    /// </summary>
    /// <param name="filters">Anonymous object having property names
    /// matching the filtering properties of the entity.</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<T?> FindOne(object? filters = null, CancellationToken ct = default);

    /// <summary>
    /// Find the multiple rows for a given entity and filtering criteria.
    /// </summary>
    /// <param name="filters">Anonymous object having property names
    /// matching the filtering properties of the entity.</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IReadOnlyList<T>> FindMany(object? filters = null, CancellationToken ct = default);
}
