namespace RagTools.Core.Primitives;

/// <summary>
/// Formatter for batch IDs returned in <c>BatchIngestResponse.BatchId</c>.
/// Format: <c>batch:{collection}:{ticks}</c>.
/// </summary>
public static class IngestBatchId
{
    public static string Create(CollectionName collection, long ticks) =>
        $"batch:{collection.Value}:{ticks}";
}
