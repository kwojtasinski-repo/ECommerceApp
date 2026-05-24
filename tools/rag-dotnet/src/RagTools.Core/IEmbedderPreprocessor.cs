namespace RagTools.Core;

/// <summary>
/// Transforms text BEFORE it is passed to the underlying embedding model.
/// Implementations are ordered and called sequentially by <see cref="PipelinedEmbedder"/>.
///
/// Contract:
/// · NEVER throw on valid text.  Invalid / empty input should be returned as-is or
///   sanitised, not rejected.  Throwing here gives the MCP client a poor error UX.
/// · Use <paramref name="ctx"/> to skip transforms that are not applicable to the
///   current purpose (e.g. <see cref="EmbedPurpose.Ingest"/> → skip glossary expansion).
/// </summary>
public interface IEmbedderPreprocessor
{
    Task<string> ProcessAsync(string text, EmbedContext ctx, CancellationToken ct = default);
}
