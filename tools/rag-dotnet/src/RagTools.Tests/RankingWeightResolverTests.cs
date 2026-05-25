using RagTools.Core;
using Xunit;

namespace RagTools.Tests;

public class RankingWeightResolverTests
{
    private static RankingSection Ranking(int stubThreshold, params (string Pattern, float Weight)[] entries)
    {
        var s = new RankingSection { StubByteThreshold = stubThreshold };
        foreach (var (p, w) in entries)
            s.Weights.Add(new WeightEntry { Pattern = p, Weight = w });
        return s;
    }

    [Fact]
    public void Resolve_NoMatchingGlob_ReturnsOne()
    {
        var r = Ranking(400);
        Assert.Equal(1.0f, RankingWeightResolver.Resolve("docs/random.md", 5000, r));
    }

    [Fact]
    public void Resolve_FirstMatchingGlobWins()
    {
        var r = Ranking(400,
            ("docs/adr/**", 1.20f),
            ("docs/**",     1.10f));
        Assert.Equal(1.20f, RankingWeightResolver.Resolve("docs/adr/0007/0007-x.md", 5000, r));
    }

    [Fact]
    public void Resolve_SecondGlobMatchesWhenFirstDoesnt()
    {
        var r = Ranking(400,
            ("docs/adr/**", 1.20f),
            ("docs/**",     1.10f));
        Assert.Equal(1.10f, RankingWeightResolver.Resolve("docs/architecture/x.md", 5000, r));
    }

    [Fact]
    public void Resolve_StubExampleImplementation_BelowThreshold_ReturnsLowWeight()
    {
        var r = Ranking(400, ("docs/**", 1.10f));
        Assert.Equal(0.05f, RankingWeightResolver.Resolve("docs/example-implementation/x.md", 200, r));
    }

    [Fact]
    public void Resolve_StubExampleImplementation_AboveThreshold_UsesGlobWeight()
    {
        var r = Ranking(400, ("docs/**", 1.10f));
        Assert.Equal(1.10f, RankingWeightResolver.Resolve("docs/example-implementation/x.md", 5000, r));
    }

    [Fact]
    public void Resolve_BackslashRelPath_NormalizedToForwardSlash()
    {
        var r = Ranking(400, ("docs/adr/**", 1.20f));
        Assert.Equal(1.20f, RankingWeightResolver.Resolve(@"docs\adr\0007\x.md", 5000, r));
    }

    [Theory]
    [InlineData("docs/x.md", "docs/*.md", true)]
    [InlineData("docs/a/x.md", "docs/*.md", false)]   // * = single segment
    [InlineData("docs/a/x.md", "docs/**", true)]      // ** = any depth
    [InlineData("docs/a/x.md", "docs/**/*.md", true)]
    [InlineData("docs/x.md",   "docs/?.md", true)]    // ? = single non-slash
    [InlineData("docs/xy.md",  "docs/?.md", false)]
    public void GlobMatch_VariousPatterns(string path, string glob, bool expected)
    {
        Assert.Equal(expected, RankingWeightResolver.GlobMatch(path, glob));
    }
}
