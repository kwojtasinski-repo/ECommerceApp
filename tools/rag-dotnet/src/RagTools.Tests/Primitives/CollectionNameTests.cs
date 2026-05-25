using RagTools.Core.Primitives;

namespace RagTools.Tests.Primitives;

/// <summary>
/// Tests for <see cref="CollectionName"/>.
/// Contract: <c>^[a-z0-9][a-z0-9_-]*$</c>, length 1..64.
/// </summary>
public sealed class CollectionNameTests
{
    [Theory]
    [InlineData("ecommerceapp_docs")]
    [InlineData("ecommerceapp_docs_dotnet")]
    [InlineData("a")]
    [InlineData("0")]
    [InlineData("abc-def_ghi")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123")] // 64 chars
    public void Parse_AcceptsValidName(string raw)
    {
        var c = CollectionName.Parse(raw);
        Assert.Equal(raw, c.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("-abc")]      // leading hyphen
    [InlineData("_abc")]      // leading underscore
    [InlineData("ABC")]       // uppercase
    [InlineData("a.b")]       // dot
    [InlineData("a b")]       // space
    [InlineData("a/b")]       // slash
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234")] // 65 chars
    public void Parse_RejectsInvalidName(string? raw)
    {
        Assert.Throws<ArgumentException>(() => CollectionName.Parse(raw));
    }

    [Theory]
    [InlineData("ecommerceapp_docs", true)]
    [InlineData("",                  false)]
    [InlineData(null,                false)]
    [InlineData("BAD",               false)]
    public void TryParse_ReturnsCorrectFlag(string? raw, bool expected)
    {
        var ok = CollectionName.TryParse(raw, out _);
        Assert.Equal(expected, ok);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var c = CollectionName.Parse("ecommerceapp_docs");
        Assert.Equal("ecommerceapp_docs", c.ToString());
    }

    [Fact]
    public void ImplicitToString_Works()
    {
        var c = CollectionName.Parse("ecommerceapp_docs");
        string s = c;
        Assert.Equal("ecommerceapp_docs", s);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = CollectionName.Parse("ecommerceapp_docs");
        var b = CollectionName.Parse("ecommerceapp_docs");
        Assert.Equal(a, b);
        Assert.True(a == b);
    }
}
