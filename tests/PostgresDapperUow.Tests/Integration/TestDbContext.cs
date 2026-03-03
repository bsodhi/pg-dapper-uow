// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using PostgresDapperUow.Abstractions;
using PostgresDapperUow.Repository;
using PostgresDapperUow.Tests.Integration;
using PostgresDapperUow.UnitOfWork;

namespace PostgresDapperUow.Tests;

internal sealed class TestDbContext : IDisposable
{
    public IUnitOfWork UnitOfWork { get; }
    public IRepository<TestUser> Users { get; }

    public TestDbContext(string connectionString)
    {
        var factory = new NpgsqlConnectionFactory(connectionString);
        UnitOfWork = new DapperUnitOfWork(factory);
        Users = new DapperRepository<TestUser>(UnitOfWork);
    }

    public void Dispose()
    {
        UnitOfWork.Dispose();
    }
}