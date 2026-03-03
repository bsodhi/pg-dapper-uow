// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Text.Json;
using PostgresDapperUow.Tests.Infrastructure;

namespace PostgresDapperUow.Tests.Integration;

public sealed class JsonMappingTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public JsonMappingTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Json_String_Should_Persist_And_Retrieve()
    {
        long id;

        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["role"] = "admin",
            ["region"] = "eu"
        });

        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            id = await ctx.Users.Create(new TestUser
            {
                Name = "JsonUser",
                Email = "json@test.com",
                Metadata = json
            });

            ctx.UnitOfWork.Commit();
        }

        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var fetched = await ctx.Users.GetById(id);

            Assert.NotNull(fetched);
            var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            var actual = JsonSerializer.Deserialize<Dictionary<string, string>>(fetched!.Metadata!);

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public async Task Null_Json_Should_Be_Persisted_As_Null()
    {
        long id;

        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            id = await ctx.Users.Create(new TestUser
            {
                Name = "NullJson",
                Email = "null@test.com",
                Metadata = null
            });

            ctx.UnitOfWork.Commit();
        }

        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var fetched = await ctx.Users.GetById(id);

            Assert.Null(fetched!.Metadata);
        }
    }

    [Fact]
    public async Task Json_Filtering_Should_Work_With_String()
    {
        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            await ctx.Users.Create(new TestUser
            {
                Name = "JsonFilter",
                Email = "filter@test.com",
                Metadata = """{"role":"admin"}"""
            });

            ctx.UnitOfWork.Commit();
        }

        using (var ctx = new TestDbContext(_fixture.ConnectionString))
        {
            var results = await ctx.Users.FindMany(
                new { Metadata = """{"role":"admin"}""" });

            Assert.Single(results);
        }
    }
}