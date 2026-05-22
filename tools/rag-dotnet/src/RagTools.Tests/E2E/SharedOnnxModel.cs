using System.Diagnostics;
using RagTools.Core;

namespace RagTools.Tests.E2E;

/// <summary>
/// Process-wide ONNX embedder singleton — loaded and warmed up ONCE, reused by all test fixtures.
///
/// Problem it solves:
///   IngestE2EFixture and HttpIngestE2EFixture each used to call OnnxEmbedder.Load() separately.
///   xUnit runs test classes in parallel by default, so both fixtures loaded the model
///   simultaneously, each creating a separate InferenceSession.  Two parallel ONNX inference
///   sessions compete for CPU — the first embedding call on each took 40–90 s instead of ~1 s.
///   This caused UploadAndWaitAsync (45 s timeout) to return null on the first N tests.
///
/// How it works:
///   On first access, Load() is called under a lock (LazyThreadSafetyMode.ExecutionAndPublication).
///   A warm-up inference is performed immediately so the JIT-compiled ONNX computation
///   graph is ready before any test starts.  Subsequent accesses return the same instance
///   in ~0 ms.
///
///   Both IngestE2EFixture and HttpIngestE2EFixture call SharedOnnxModel.Instance instead of
///   OnnxEmbedder.Load(). The second call is instant — no double load, no CPU contention.
/// </summary>
internal static class SharedOnnxModel
{
    private static readonly Lazy<OnnxEmbedder> _embedder =
        new(LoadAndWarmUp, LazyThreadSafetyMode.ExecutionAndPublication);

    public static readonly string ModelDir =
        Environment.GetEnvironmentVariable("RAG_MODEL_DIR")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "model"));

    /// <summary>True when model.onnx exists in ModelDir — same guard used by both fixtures.</summary>
    public static bool IsAvailable => File.Exists(Path.Combine(ModelDir, "model.onnx"));

    /// <summary>
    /// Returns the shared, warmed-up <see cref="OnnxEmbedder"/>.
    /// First access blocks until Load + warm-up complete (~1–5 s when model is OS-cached,
    /// or longer on first ever load).  Subsequent accesses return immediately.
    /// </summary>
    public static OnnxEmbedder Instance => _embedder.Value;

    private static OnnxEmbedder LoadAndWarmUp()
    {
        Console.WriteLine($"[SharedOnnxModel] Loading ONNX model from {ModelDir} ...");
        var sw = Stopwatch.StartNew();

        var embedder = OnnxEmbedder.Load(ModelDir);
        Console.WriteLine($"[SharedOnnxModel] Model loaded in {sw.Elapsed.TotalSeconds:F1}s" +
                          $"  (dims={embedder.Dimensions})");

        Console.WriteLine("[SharedOnnxModel] Running warm-up inference ...");
        embedder.EmbedBatch(["warm-up"]);
        Console.WriteLine($"[SharedOnnxModel] Warm-up complete. Total init: {sw.Elapsed.TotalSeconds:F1}s");

        return embedder;
    }
}
