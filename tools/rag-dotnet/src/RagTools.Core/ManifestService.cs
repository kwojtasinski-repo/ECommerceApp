using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RagTools.Core;

/// <summary>
/// Tracks file hashes to enable incremental ingest.
/// Reads and writes .rag/manifest.json at the repo root.
/// Format matches the Python implementation for interoperability.
/// </summary>
public sealed class ManifestService
{
    private readonly string _manifestPath;
    private Dictionary<string, ManifestEntry> _entries;

    private ManifestService(string manifestPath, Dictionary<string, ManifestEntry> entries)
    {
        _manifestPath = manifestPath;
        _entries = entries;
    }

    public static ManifestService Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return new ManifestService(manifestPath, []);

        var json = File.ReadAllText(manifestPath);
        var entries = JsonSerializer.Deserialize<Dictionary<string, ManifestEntry>>(json,
            JsonOptions) ?? [];
        return new ManifestService(manifestPath, entries);
    }

    /// <summary>Returns the SHA-256 hex hash of a file, or null if the file doesn't exist.</summary>
    public static string? HashFile(string absolutePath)
    {
        if (!File.Exists(absolutePath)) return null;
        var bytes = File.ReadAllBytes(absolutePath);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public bool IsUnchanged(string relPath, string currentHash)
    {
        return _entries.TryGetValue(relPath, out var entry)
            && entry.Hash == currentHash;
    }

    public void Update(string relPath, string hash, int chunkCount)
    {
        _entries[relPath] = new ManifestEntry(hash, chunkCount, DateTimeOffset.UtcNow);
    }

    public void Remove(string relPath) => _entries.Remove(relPath);

    /// <summary>All manifest entries — used for stats generation.</summary>
    public IEnumerable<(string RelPath, ManifestEntry Entry)> All() =>
        _entries.Select(kv => (kv.Key, kv.Value));

    /// <summary>Number of tracked files.</summary>
    public int FileCount => _entries.Count;

    /// <summary>Returns rel_paths that are in the manifest but no longer present in the file set.</summary>
    public IEnumerable<string> FindDeleted(IEnumerable<string> currentRelPaths)
    {
        var current = currentRelPaths.ToHashSet();
        return _entries.Keys.Where(k => !current.Contains(k));
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(_manifestPath);
        if (dir is not null) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_entries, JsonOptions);
        File.WriteAllText(_manifestPath, json);
    }

    /// <summary>Stable UUID derived from rel_path + breadcrumb + start_line. Matches Python implementation.</summary>
    public static Guid StableId(string relPath, string breadcrumb, int startLine)
    {
        var key = $"{relPath}::{breadcrumb}::{startLine}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed record ManifestEntry(
    [property: JsonPropertyName("hash")] string Hash,
    [property: JsonPropertyName("chunk_count")] int ChunkCount,
    [property: JsonPropertyName("indexed_at")] DateTimeOffset IndexedAt);
