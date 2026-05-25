using RagTools.Core.Primitives;

namespace RagTools.Tests.Primitives;

/// <summary>
/// Format contract: <c>batch:{collection}:{ticks}</c>
/// </summary>
public sealed class IngestBatchIdTests
{
    [Fact]
    public void Create_ProducesCanonicalFormat()
    {
        var coll = CollectionName.Parse("ecommerceapp_docs");
        var id = IngestBatchId.Create(coll, ticks: 638_000_000_000_000_000L);
        Assert.Equal("batch:ecommerceapp_docs:638000000000000000", id);
    }

    [Fact]
    public void Create_WithZeroTicks()
    {
        var coll = CollectionName.Parse("docs");
        Assert.Equal("batch:docs:0", IngestBatchId.Create(coll, 0));
    }
}
