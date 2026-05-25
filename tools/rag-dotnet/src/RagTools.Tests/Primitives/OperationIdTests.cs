using RagTools.Core.Primitives;

namespace RagTools.Tests.Primitives;

/// <summary>
/// Format contract: <c>{collection}:{safeRelPath}:{ticks}-{index}</c>
/// where safeRelPath is relPath with '/' replaced by '-'.
/// </summary>
public sealed class OperationIdTests
{
    [Fact]
    public void Create_ProducesCanonicalFormat()
    {
        var coll = CollectionName.Parse("ecommerceapp_docs");
        var id = OperationId.Create(coll, "docs/adr/0001/adr.md", ticks: 638_000_000_000_000_000L, index: 0);
        Assert.Equal("ecommerceapp_docs:docs-adr-0001-adr.md:638000000000000000-0", id);
    }

    [Fact]
    public void Create_ReplacesAllSlashesWithDashes()
    {
        var coll = CollectionName.Parse("docs");
        var id = OperationId.Create(coll, "a/b/c/d.md", ticks: 1, index: 0);
        Assert.Equal("docs:a-b-c-d.md:1-0", id);
    }

    [Fact]
    public void Create_PreservesAlreadySafePath()
    {
        var coll = CollectionName.Parse("docs");
        var id = OperationId.Create(coll, "file.md", ticks: 1, index: 5);
        Assert.Equal("docs:file.md:1-5", id);
    }

    [Fact]
    public void Create_HandlesBackslashes()
    {
        var coll = CollectionName.Parse("docs");
        var id = OperationId.Create(coll, @"docs\adr\file.md", ticks: 1, index: 0);
        Assert.Equal("docs:docs-adr-file.md:1-0", id);
    }

    [Fact]
    public void CollectionFrom_ReturnsLeadingSegment()
    {
        Assert.Equal("ecommerceapp_docs", OperationId.CollectionFrom("ecommerceapp_docs:file.md:1-0"));
    }

    [Fact]
    public void CollectionFrom_ReturnsNullForMalformed()
    {
        Assert.Null(OperationId.CollectionFrom("no-colons-here"));
        Assert.Null(OperationId.CollectionFrom(""));
    }
}
