// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using PostgresDapperUow.Internal;

namespace PostgresDapperUow.Tests.Unit;

public sealed class PropertyAccessorFactoryTests
{
    private sealed class Test
    {
        public string Name { get; set; } = "Test";
    }

    [Fact]
    public void Getter_Returns_Value()
    {
        var prop = typeof(Test).GetProperty(nameof(Test.Name))!;
        var getter = PropertyAccessorFactory.CreateGetter(prop);

        var value = getter(new Test());

        Assert.Equal("Test", value);
    }
}