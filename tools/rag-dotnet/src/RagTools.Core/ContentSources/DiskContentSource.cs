namespace RagTools.Core.ContentSources;

/// <summary>
/// Reads document content directly from the local workspace filesystem.
/// Registered in STDIO mode where the workspace directory is always accessible.
/// Returns <c>null</c> when the file does not exist (e.g. rel_path not yet on disk).
/// </summary>
public sealed class DiskContentSource(RagConfig cfg) : IContentSource
{
    public async Task<string?> ReadAsync(string collection, string relPath, CancellationToken ct = default)
    {
        var abs = Path.GetFullPath(
            Path.Combine(cfg.Workspace, relPath.Replace('/', Path.DirectorySeparatorChar)));

        if (!File.Exists(abs))
            return null;

        return await File.ReadAllTextAsync(abs, ct);
    }
}
