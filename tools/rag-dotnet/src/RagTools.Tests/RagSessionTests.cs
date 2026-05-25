using RagTools.Core;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for <see cref="RagSession"/> with the new <see cref="ICollectionResolver"/> design.
/// </summary>
public sealed class RagSessionTests
{
    [Fact]
    public void Collection_DelegatesToResolver()
    {
        var resolver = new FixedCollectionResolver("my_collection");
        var session  = new RagSession(resolver);

        Assert.Equal("my_collection", session.Collection);
    }

    [Fact]
    public void Collection_ReflectsResolverValue_EachCall()
    {
        // FixedCollectionResolver is immutable, but this verifies multiple reads are consistent.
        var session = new RagSession(new FixedCollectionResolver("alpha"));

        Assert.Equal("alpha", session.Collection);
        Assert.Equal("alpha", session.Collection);
    }

    [Fact]
    public void TwoSessions_WithDifferentResolvers_ReturnDifferentCollections()
    {
        var s1 = new RagSession(new FixedCollectionResolver("col_a"));
        var s2 = new RagSession(new FixedCollectionResolver("col_b"));

        Assert.Equal("col_a", s1.Collection);
        Assert.Equal("col_b", s2.Collection);
        Assert.NotEqual(s1.Collection, s2.Collection);
    }
}
