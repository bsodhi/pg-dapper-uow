// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using PostgresDapperUow.Exceptions;
using PostgresDapperUow.Tests.Infrastructure;

namespace PostgresDapperUow.Tests.Integration;

public sealed class ConcurrencyTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public ConcurrencyTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Concurrent_Update_Should_Throw_ConcurrencyException()
    {
        long id;

        // Seed entity
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var user = new TestUser
            {
                Name = "Initial",
                Email = "initial@test.com"
            };

            id = await ctx.Users.Create(user);
            ctx.UnitOfWork.Commit();
        }

        // Open two independent transactions
        using var ctxA = new TestDbContext(_fixture.ConnectionString);
        using var ctxB = new TestDbContext(_fixture.ConnectionString);

        var entityA = await ctxA.Users.GetById(id);
        var entityB = await ctxB.Users.GetById(id);

        Assert.NotNull(entityA);
        Assert.NotNull(entityB);

        // First update succeeds
        entityA!.Name = "Updated A";
        await ctxA.Users.Update(entityA);
        ctxA.UnitOfWork.Commit();

        // Second update should fail
        entityB!.Name = "Updated B";

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            ctxB.Users.Update(entityB));
    }

    [Fact]
    public async Task Successful_Update_Should_Increment_TxnNo()
    {
        long id;

        // Insert
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            id = await ctx.Users.Create(new TestUser
            {
                Name = "TxnTest",
                Email = "txn@test.com"
            });

            ctx.UnitOfWork.Commit();
        }

        int originalTxn;

        // Load persisted entity (ensures we get DB default txn_no)
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var entity = await ctx.Users.GetById(id);

            Assert.NotNull(entity);
            originalTxn = entity!.TxnNo;
        }

        // Perform update in a fresh transaction
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var entity = await ctx.Users.GetById(id);

            entity!.Name = "TxnUpdated";

            await ctx.Users.Update(entity);
            ctx.UnitOfWork.Commit();
        }

        // Verify increment
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var updated = await ctx.Users.GetById(id);

            Assert.NotNull(updated);
            Assert.Equal(originalTxn + 1, updated!.TxnNo);
        }
    }
}