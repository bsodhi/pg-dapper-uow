// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Data;
using Npgsql;
using PostgresDapperUow.Abstractions;

namespace PostgresDapperUow.UnitOfWork;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(string connectionString, 
        Action<NpgsqlDataSourceBuilder>? action = null)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        action?.Invoke(builder);
        _dataSource = builder.Build();
    }

    public IDbConnection OpenConnection()
    {
        // Returned connection already knows about enums
        return _dataSource.OpenConnection();
    }

    public void Dispose()
    {
        _dataSource.Dispose();
    }
}

