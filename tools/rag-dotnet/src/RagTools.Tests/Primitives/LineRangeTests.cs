using RagTools.Core.Primitives;

namespace RagTools.Tests.Primitives;

public sealed class LineRangeTests
{
    [Fact]
    public void Ctor_AcceptsValidRange()
    {
        var r = new LineRange(1, 5);
        Assert.Equal(1, r.StartLine);
        Assert.Equal(5, r.EndLine);
        Assert.Equal(5, r.Length);
    }

    [Fact]
    public void Ctor_AcceptsSingleLine()
    {
        var r = new LineRange(7, 7);
        Assert.Equal(1, r.Length);
    }

    [Fact]
    public void Ctor_RejectsStartLessThanOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineRange(0, 5));
    }

    [Fact]
    public void Ctor_RejectsEndBeforeStart()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineRange(10, 5));
    }

    [Theory]
    [InlineData(1, 10, 1,  true)]
    [InlineData(1, 10, 10, true)]
    [InlineData(1, 10, 5,  true)]
    [InlineData(1, 10, 11, false)]
    [InlineData(1, 10, 0,  false)]
    public void Contains_ChecksBoundaries(int start, int end, int probe, bool expected)
    {
        var r = new LineRange(start, end);
        Assert.Equal(expected, r.Contains(probe));
    }

    [Theory]
    [InlineData(1, 5,  3, 7,  true)]   // partial overlap
    [InlineData(1, 5,  5, 9,  true)]   // touches at boundary
    [InlineData(1, 5,  6, 9,  false)]  // disjoint
    [InlineData(3, 5,  1, 9,  true)]   // contained
    public void Overlaps_HandlesCases(int aS, int aE, int bS, int bE, bool expected)
    {
        var a = new LineRange(aS, aE);
        var b = new LineRange(bS, bE);
        Assert.Equal(expected, a.Overlaps(b));
        Assert.Equal(expected, b.Overlaps(a));
    }

    [Fact]
    public void ToString_FormatsAsLstartLend()
    {
        Assert.Equal("L10-L20", new LineRange(10, 20).ToString());
    }
}
