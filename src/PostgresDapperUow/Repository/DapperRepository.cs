// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Data;
using System.Text;
using System.Text.Json;
using Dapper;
using PostgresDapperUow.Abstractions;
using PostgresDapperUow.Attributes;
using PostgresDapperUow.Exceptions;
using PostgresDapperUow.Internal;
using PostgresDapperUow.Mapping;

namespace PostgresDapperUow.Repository;

/// <summary>
/// Performs one-time initialization of Dapper configuration required by the
/// PostgresDapperUow infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This class ensures that Dapper is configured for snake_case to PascalCase
/// property mapping by enabling <see cref="DefaultTypeMap.MatchNamesWithUnderscores"/>.
/// </para>
///
/// <para>
/// The initialization is idempotent and thread-safe. It executes only once
/// per application lifetime, regardless of how many repositories are created.
/// </para>
///
/// <para>
/// This internal bootstrap avoids placing configuration burden on consumers
/// of the library and guarantees consistent behavior in all hosting scenarios
/// (web applications, background services, console applications, and tests).
/// </para>
/// </remarks>
internal static class DapperBootstrap
{
    private static int _initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}

/// <summary>
/// Provides a transactional PostgreSQL repository implementation using Dapper.
/// </summary>
/// <typeparam name="T">
/// The entity type mapped to a database table.
/// </typeparam>
/// <remarks>
/// <para>
/// <see cref="DapperRepository{T}"/> implements a lightweight repository pattern
/// with explicit transaction boundaries enforced via <see cref="IUnitOfWork"/>.
/// </para>
///
/// <para>
/// Key characteristics:
/// </para>
/// <list type="bullet">
/// <item>PostgreSQL-optimized SQL generation</item>
/// <item>Attribute-based table and column mapping</item>
/// <item>Optimistic concurrency support via <c>txn_no</c></item>
/// <item>Compiled property accessors (no runtime reflection)</item>
/// <item>Fully parameterized queries</item>
/// </list>
///
/// <para>
/// A repository instance is bound to a single <see cref="IUnitOfWork"/> and must
/// not be used after the unit of work has been committed or rolled back.
/// </para>
///
/// <para>
/// This class is designed for predictable behavior, explicit transaction control,
/// and minimal abstraction overhead. It is not a full ORM.
/// </para>
/// </remarks>
public sealed class DapperRepository<T> : IRepository<T>
{
    private readonly IDbConnection _connection;
    private readonly IDbTransaction _transaction;
    private readonly string _table;
    private readonly IReadOnlyList<ColumnMap> _columns;
    private readonly string _selectColumns;
    private static readonly bool _isAudited =
        typeof(BaseEntity).IsAssignableFrom(typeof(T));

    public DapperRepository(IUnitOfWork uow)
    {
        ArgumentNullException.ThrowIfNull(uow);
        DapperBootstrap.EnsureInitialized();

        _connection = uow.Connection;
        _transaction = uow.Transaction;

        var tableName =
            typeof(T).GetCustomAttributes(typeof(TableAttribute), false)
                .Cast<TableAttribute>()
                .FirstOrDefault()?.Name
            ?? Naming.ToSnakeCase(typeof(T).Name);

        _table = $"\"{tableName}\"";

        _columns = ColumnMapCache.For<T>();

        _selectColumns = string.Join(", ",
            _columns.Select(c => $"\"{c.ColumnName}\""));

    }

    private void EnsureActiveTransaction()
    {
        if (_transaction.Connection is null)
            throw new InvalidOperationException(
                "The underlying transaction has already been completed. " +
                "Repositories cannot be used after UnitOfWork.Commit() or Rollback().");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<T?> GetById(long id, CancellationToken ct = default)
    {
        EnsureActiveTransaction();
        var sql = $"SELECT {_selectColumns} FROM {_table} WHERE id = @id";

        var entity = await _connection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(sql, new { id }, _transaction, cancellationToken: ct));
        return entity;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<long> Create(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureActiveTransaction();
        var insertable = _columns
            .Where(c => c.Insertable && c.Property.Name != nameof(BaseEntity.Id))
            .ToList();

        var columnList = string.Join(", ",
            insertable.Select(c => QuoteIdentifier(c.ColumnName)));

        var valuesList = string.Join(", ",
            insertable.Select(c => c.SqlParameter));

        var sql = $"""
            INSERT INTO {_table} ({columnList})
            VALUES ({valuesList})
            RETURNING id
            """;

        var parameters = BuildParameters(entity);

        return await _connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, parameters, _transaction, cancellationToken: ct));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="ConcurrencyException"></exception>
    public async Task<bool> Update(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureActiveTransaction();
        var updatable = _columns
            .Where(c => c.Updatable)
            .Where(c => !_isAudited ||
                        c.Property.Name is not (nameof(BaseEntity.Id)
                            or nameof(BaseEntity.InsertedAt)
                            or nameof(BaseEntity.TxnNo)
                            or nameof(BaseEntity.UpdatedAt)))
            .ToList();

        var setClause = new StringBuilder();

        foreach (var col in updatable)
        {
            setClause.Append($"{QuoteIdentifier(col.ColumnName)} = {col.SqlParameter}, ");
        }

        if (_isAudited)
        {
            setClause.Append("updated_at = now(), txn_no = txn_no + 1");
        }
        else
        {
            if (setClause.Length >= 2)
                setClause.Length -= 2; // remove trailing comma
        }

        var sql = _isAudited
            ? $"""
               UPDATE {_table}
               SET {setClause}
               WHERE id = @Id
                 AND txn_no = @TxnNo
               """
            : $"""
               UPDATE {_table}
               SET {setClause}
               WHERE id = @Id
               """;

        var parameters = BuildParameters(entity);
        var affected = await _connection.ExecuteAsync(
            new CommandDefinition(sql, parameters, _transaction, cancellationToken: ct));

        if (affected == 0)
        {
            if (_isAudited)
                throw new ConcurrencyException(typeof(T).Name);

            return false;
        }

        return true;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> DeleteById(long id, CancellationToken ct = default)
    {
        EnsureActiveTransaction();
        var sql = $"DELETE FROM {_table} WHERE id = @id";

        var affected = await _connection.ExecuteAsync(
            new CommandDefinition(sql, new { id }, _transaction, cancellationToken: ct));

        return affected == 1;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filters"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<T?> FindOne(object? filters = null, CancellationToken ct = default)
    {
        EnsureActiveTransaction();
        var (whereSql, parameters) = WhereObjectBuilder.Build<T>(filters);

        var sql = $"SELECT {_selectColumns} FROM {_table} {whereSql}";

        return await _connection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(sql, parameters, _transaction, cancellationToken: ct));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filters"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<T>> FindMany(object? filters = null, CancellationToken ct = default)
    {
        EnsureActiveTransaction();
        var (whereSql, parameters) = WhereObjectBuilder.Build<T>(filters);

        var sql = $"SELECT {_selectColumns} FROM {_table} {whereSql} ORDER BY id";

        var rows = await _connection.QueryAsync<T>(
            new CommandDefinition(sql, parameters, _transaction, cancellationToken: ct));

        return rows.AsList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    private DynamicParameters BuildParameters(object entity)
    {
        var parameters = new DynamicParameters();

        foreach (var col in _columns)
        {
            var value = col.Property.GetValue(entity);

            if (value is null)
            {
                parameters.Add(col.Property.Name, null);
                continue;
            }

            if (value is DateOnly dateOnly)
            {
                parameters.Add(col.Property.Name, dateOnly.ToDateTime(TimeOnly.MinValue));
                continue;
            }

            if (col.Property.PropertyType.IsEnum)
            {
                parameters.Add(col.Property.Name, value.ToString());
                continue;
            }

            if (col.IsJson)
            {
                parameters.Add(col.Property.Name,
                    value is string s ? s : JsonSerializer.Serialize(value));
                continue;
            }

            parameters.Add(col.Property.Name, value);
        }

        return parameters;
    }

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier}\"";
}