// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------


using PostgresDapperUow.Internal;

namespace PostgresDapperUow.Tests.Unit;

public sealed class WhereObjectBuilderTests
{
    private sealed class Dummy
    {
        public string Name { get; set; } = default!;
        public int Age { get; set; }
    }

    [Fact]
    public void Null_Filter_Returns_Empty()
    {
        var (sql, _) = WhereObjectBuilder.Build<Dummy>(null);
        Assert.Equal("", sql);
    }

    [Fact]
    public void Simple_Filter_Builds_Where()
    {
        var (sql, _) = WhereObjectBuilder.Build<Dummy>(
            new { Name = "Alice" });

        Assert.Contains("WHERE", sql);
        Assert.Contains("Name", sql);
    }

    [Fact]
    public void Null_Value_Generates_IsNull()
    {
        var (sql, _) = WhereObjectBuilder.Build<Dummy>(
            new { Name = (string?)null });

        Assert.Contains("IS NULL", sql);
    }

    [Fact]
    public void Collection_Filter_Uses_Any()
    {
        var (sql, _) = WhereObjectBuilder.Build<Dummy>(
            new { Age = new[] { 1, 2, 3 } });

        Assert.Contains("ANY", sql);
    }
}