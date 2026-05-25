namespace RagTools.Core.Primitives;

/// <summary>
/// Formatter for operation IDs used to track per-file ingest jobs.
/// Format: <c>{collection}:{safeRelPath}:{ticks}-{index}</c>
/// where safeRelPath has forward slashes replaced with dashes so the ID is URL-path-safe.
/// </summary>
public static class OperationId
{
    public static string Create(CollectionName collection, string relPath, long ticks, int index)
    {
        var safeRelPath = relPath.Replace('\\', '/').Replace('/', '-');
        return $"{collection.Value}:{safeRelPath}:{ticks}-{index}";
    }

    /// <summary>
    /// Parse the leading collection segment from an operation ID. Returns null if malformed.
    /// </summary>
    public static string? CollectionFrom(string operationId)
    {
        if (string.IsNullOrEmpty(operationId))
        {
            return null;
        }

        var idx = operationId.IndexOf(':');
        return idx > 0 ? operationId[..idx] : null;
    }
}
