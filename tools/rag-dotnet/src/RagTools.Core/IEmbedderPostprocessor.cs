namespace RagTools.Core;

/// <summary>
/// Transforms a <c>float[]</c> vector AFTER it is produced by the underlying embedding model.
/// Implementations are ordered and called sequentially by <see cref="PipelinedEmbedder"/>.
///
/// Examples:
/// · L2 normalisation (force unit-length vectors when the model does not normalise).
/// · Zero-vector detection (log a warning if the model returned an all-zero embedding).
/// · Dimension validation (throw if the returned vector has an unexpected length).
///
/// No built-in implementation ships with this change — this is a pure extension point.
/// </summary>
public interface IEmbedderPostprocessor
{
    Task<float[]> ProcessAsync(float[] vector, EmbedContext ctx, CancellationToken ct = default);
}
