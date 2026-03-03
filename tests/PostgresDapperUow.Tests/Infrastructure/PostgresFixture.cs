// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using Testcontainers.PostgreSql;
using Npgsql;
using Dapper;

namespace PostgresDapperUow.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public string ConnectionString => _container.GetConnectionString();

    public PostgresFixture()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var sql = """
            CREATE TABLE users (
                id BIGSERIAL PRIMARY KEY,
                txn_no INT NOT NULL DEFAULT 1,
                inserted_at TIMESTAMP NOT NULL DEFAULT now(),
                updated_at TIMESTAMP NOT NULL DEFAULT now(),
                name TEXT NOT NULL,
                email TEXT NOT NULL,
                metadata JSONB NULL
            );
            """;

        await conn.ExecuteAsync(sql);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}