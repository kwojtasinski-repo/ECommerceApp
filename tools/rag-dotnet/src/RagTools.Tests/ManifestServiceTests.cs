using System.Text.Json;
using RagTools.Core;
using Xunit;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for ManifestService.
/// All I/O uses a temp directory that is cleaned up after each test.
/// </summary>
public class ManifestServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _manifestPath;

    public ManifestServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _manifestPath = Path.Combine(_tempDir, "manifest.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── Load ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_NonExistentFile_ReturnsEmptyManifest()
    {
        var svc = ManifestService.Load(_manifestPath);
        Assert.Equal(0, svc.FileCount);
    }

    [Fact]
    public void Load_ExistingFile_RestoresEntries()
    {
        // Arrange: write a minimal valid manifest
        var json = """
            {
              "docs/adr/001.md": {
                "hash": "abc123",
                "chunk_count": 3,
                "indexed_at": "2024-01-01T00:00:00+00:00"
              }
            }
            """;
        File.WriteAllText(_manifestPath, json);

        // Act
        var svc = ManifestService.Load(_manifestPath);

        // Assert
        Assert.Equal(1, svc.FileCount);
    }

    // ── HashFile ─────────────────────────────────────────────────────────────

    [Fact]
    public void HashFile_NonExistentFile_ReturnsNull()
    {
        var result = ManifestService.HashFile("/nonexistent/file.md");
        Assert.Null(result);
    }

    [Fact]
    public void HashFile_ExistingFile_ReturnsSha256HexLower()
    {
        var filePath = Path.Combine(_tempDir, "test.md");
        File.WriteAllText(filePath, "hello");

        var hash = ManifestService.HashFile(filePath);

        Assert.NotNull(hash);
        Assert.Equal(64, hash!.Length); // SHA-256 → 32 bytes → 64 hex chars
        Assert.Equal(hash, hash.ToLowerInvariant()); // must be lowercase
    }

    [Fact]
    public void HashFile_SameContent_ReturnsSameHash()
    {
        var a = Path.Combine(_tempDir, "a.md");
        var b = Path.Combine(_tempDir, "b.md");
        File.WriteAllText(a, "hello world");
        File.WriteAllText(b, "hello world");

        Assert.Equal(ManifestService.HashFile(a), ManifestService.HashFile(b));
    }

    [Fact]
    public void HashFile_DifferentContent_ReturnsDifferentHash()
    {
        var a = Path.Combine(_tempDir, "a.md");
        var b = Path.Combine(_tempDir, "b.md");
        File.WriteAllText(a, "hello");
        File.WriteAllText(b, "world");

        Assert.NotEqual(ManifestService.HashFile(a), ManifestService.HashFile(b));
    }

    // ── IsUnchanged ───────────────────────────────────────────────────────────

    [Fact]
    public void IsUnchanged_UnknownFile_ReturnsFalse()
    {
        var svc = ManifestService.Load(_manifestPath);
        Assert.False(svc.IsUnchanged("docs/missing.md", "abc"));
    }

    [Fact]
    public void IsUnchanged_KnownFileCorrectHash_ReturnsTrue()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("docs/file.md", "abc123", 5);

        Assert.True(svc.IsUnchanged("docs/file.md", "abc123"));
    }

    [Fact]
    public void IsUnchanged_KnownFileWrongHash_ReturnsFalse()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("docs/file.md", "abc123", 5);

        Assert.False(svc.IsUnchanged("docs/file.md", "different"));
    }

    // ── Update / Remove / FileCount ───────────────────────────────────────────

    [Fact]
    public void Update_AddsNewEntry()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("docs/a.md", "hash1", 3);

        Assert.Equal(1, svc.FileCount);
        Assert.True(svc.IsUnchanged("docs/a.md", "hash1"));
    }

    [Fact]
    public void Update_OverwritesExistingEntry()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("docs/a.md", "hash1", 3);
        svc.Update("docs/a.md", "hash2", 7);

        Assert.Equal(1, svc.FileCount);
        Assert.True(svc.IsUnchanged("docs/a.md", "hash2"));
        Assert.False(svc.IsUnchanged("docs/a.md", "hash1"));
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("docs/a.md", "h", 1);
        svc.Remove("docs/a.md");

        Assert.Equal(0, svc.FileCount);
    }

    [Fact]
    public void Remove_NonExistentKey_DoesNotThrow()
    {
        var svc = ManifestService.Load(_manifestPath);
        var ex = Record.Exception(() => svc.Remove("docs/never-added.md"));
        Assert.Null(ex);
    }

    // ── Save / round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void Save_WritesJsonFile()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("docs/a.md", "deadbeef", 2);
        svc.Save();

        Assert.True(File.Exists(_manifestPath));
        var json = File.ReadAllText(_manifestPath);
        Assert.Contains("deadbeef", json);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsEntries()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("docs/a.md", "hash1", 3);
        svc.Update("docs/b.md", "hash2", 5);
        svc.Save();

        var svc2 = ManifestService.Load(_manifestPath);
        Assert.Equal(2, svc2.FileCount);
        Assert.True(svc2.IsUnchanged("docs/a.md", "hash1"));
        Assert.True(svc2.IsUnchanged("docs/b.md", "hash2"));
    }

    [Fact]
    public void Save_CreatesParentDirectoryIfMissing()
    {
        var deepPath = Path.Combine(_tempDir, "subdir", "manifest.json");
        var svc = ManifestService.Load(deepPath);
        svc.Update("docs/a.md", "h", 1);
        svc.Save();

        Assert.True(File.Exists(deepPath));
    }

    // ── FindDeleted ───────────────────────────────────────────────────────────

    [Fact]
    public void FindDeleted_AllFilesStillPresent_ReturnsEmpty()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("a.md", "h1", 1);
        svc.Update("b.md", "h2", 1);

        var deleted = svc.FindDeleted(["a.md", "b.md"]).ToList();
        Assert.Empty(deleted);
    }

    [Fact]
    public void FindDeleted_MissingFile_ReturnsIt()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("a.md", "h1", 1);
        svc.Update("b.md", "h2", 1);

        // "b.md" has been deleted from disk.
        var deleted = svc.FindDeleted(["a.md"]).ToList();
        Assert.Equal(["b.md"], deleted);
    }

    [Fact]
    public void FindDeleted_EmptyCurrentSet_ReturnsAllTracked()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("a.md", "h1", 1);
        svc.Update("b.md", "h2", 1);

        var deleted = svc.FindDeleted([]).ToList();
        Assert.Equal(2, deleted.Count);
    }

    // ── All ───────────────────────────────────────────────────────────────────

    [Fact]
    public void All_ReturnsAllTrackedEntries()
    {
        var svc = ManifestService.Load(_manifestPath);
        svc.Update("a.md", "h1", 1);
        svc.Update("b.md", "h2", 2);

        var all = svc.All().ToList();
        Assert.Equal(2, all.Count);
    }

    // ── StableId ──────────────────────────────────────────────────────────────

    [Fact]
    public void StableId_SameInputs_ReturnsSameGuid()
    {
        var id1 = ManifestService.StableId("docs/a.md", "Doc > Section", 42);
        var id2 = ManifestService.StableId("docs/a.md", "Doc > Section", 42);
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void StableId_DifferentPath_ReturnsDifferentGuid()
    {
        var id1 = ManifestService.StableId("docs/a.md", "breadcrumb", 1);
        var id2 = ManifestService.StableId("docs/b.md", "breadcrumb", 1);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void StableId_DifferentBreadcrumb_ReturnsDifferentGuid()
    {
        var id1 = ManifestService.StableId("docs/a.md", "Section A", 1);
        var id2 = ManifestService.StableId("docs/a.md", "Section B", 1);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void StableId_DifferentStartLine_ReturnsDifferentGuid()
    {
        var id1 = ManifestService.StableId("docs/a.md", "breadcrumb", 1);
        var id2 = ManifestService.StableId("docs/a.md", "breadcrumb", 2);
        Assert.NotEqual(id1, id2);
    }
}
