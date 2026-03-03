// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Text.Json;
using PostgresDapperUow.Exceptions;
using PostgresDapperUow.Tests.Infrastructure;

namespace PostgresDapperUow.Tests.Integration;

public sealed class RepositoryTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_And_GetById_Works()
    {
        long id;

        // Arrange + Act
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var user = new TestUser
            {
                Name = "Alice",
                Email = "alice@test.com"
            };

            id = await ctx.Users.Create(user);
            ctx.UnitOfWork.Commit();
        }

        // Assert in new transaction
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var fetched = await ctx.Users.GetById(id);

            Assert.NotNull(fetched);
            Assert.Equal("Alice", fetched!.Name);
        }
    }

    [Fact]
    public async Task Update_With_Concurrency_Works()
    {
        long id;

        // Seed entity
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var user = new TestUser
            {
                Name = "Bob",
                Email = "bob@test.com"
            };

            id = await ctx.Users.Create(user);
            ctx.UnitOfWork.Commit();
        }

        // Two independent transactions
        using var ctxA = new TestDbContext(_fixture.ConnectionString);
        using var ctxB = new TestDbContext(_fixture.ConnectionString);

        var entityA = await ctxA.Users.GetById(id);
        var entityB = await ctxB.Users.GetById(id);

        entityA!.Name = "Bob A";
        await ctxA.Users.Update(entityA);
        ctxA.UnitOfWork.Commit();

        entityB!.Name = "Bob B";

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            ctxB.Users.Update(entityB));

        ctxB.UnitOfWork.Rollback();
    }

    [Fact]
    public async Task JsonColumn_Is_Serialized()
    {
        long id;

        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var user = new TestUser
            {
                Name = "JsonUser",
                Email = "json@test.com",
                Metadata = """{"role":"admin"}"""
            };

            id = await ctx.Users.Create(user);
            ctx.UnitOfWork.Commit();
        }

        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var fetched = await ctx.Users.GetById(id);

            Assert.NotNull(fetched!.Metadata);
            Assert.Contains("admin", fetched.Metadata);
            
            var dict = JsonSerializer.Deserialize<Dictionary<string,string>>(fetched.Metadata!);
            Assert.Equal("admin", dict!["role"]);
        }
    }

    [Fact]
    public async Task FindMany_Filter_Works()
    {
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            await ctx.Users.Create(new TestUser { Name = "X", Email = "x@test.com" });
            await ctx.Users.Create(new TestUser { Name = "Y", Email = "y@test.com" });

            ctx.UnitOfWork.Commit();
        }

        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var results = await ctx.Users.FindMany(new { Name = "X" });

            Assert.Single(results);
        }
    }

    [Fact]
    public async Task Rollback_Prevents_Persist()
    {
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            await ctx.Users.Create(new TestUser
            {
                Name = "Temp",
                Email = "temp@test.com"
            });

            ctx.UnitOfWork.Rollback();
        }

        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var result = await ctx.Users.FindMany(new { Name = "Temp" });

            Assert.Empty(result);
        }
    }
}