using System.Diagnostics;
using RagTools.Core;
using Xunit;

namespace RagTools.Tests.E2E;

// ──────────────────────────────────────────────────────────────────────────────
// Collection fixture — ONNX model shared across ALL test classes in the
// "Rag E2E" collection.
//
// xUnit guarantees:
//   • One instance of SharedOnnxFixture per test run (not per class).
//   • InitializeAsync() finishes BEFORE the first test in the collection starts.
//   • DisposeAsync() runs AFTER the last test in the collection finishes.
//   • All tests in the collection run SEQUENTIALLY (no inter-class parallelism).
//
// Why a collection fixture and not just a static field?
//   The collection fixture participates properly in xUnit's lifetime management
//   and is the recommended pattern for sharing expensive resources.  The static
//   SharedOnnxModel backing store is used here so the model is lazily loaded
//   on first access and the same InferenceSession is returned every time —
//   even if two class fixtures tried to load it in parallel during a previous
//   design.
//
// Logging:
//   This fixture owns a XunitLogSink.  Each test class constructor calls
//   Sink.SetOutput(output) to redirect messages to that test's xUnit output.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// xUnit collection fixture that loads and warm-ups the ONNX embedder model
/// ONCE for the entire "Rag E2E" test collection.
///
/// Inject via constructor: <c>SharedOnnxFixture onnx</c> in any test class
/// decorated with <c>[Collection(RagTestCollection.Name)]</c>.
/// </summary>
public sealed class SharedOnnxFixture : IAsyncLifetime
{
    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>
    /// The shared, warmed-up embedder.  Null when <see cref="IsAvailable"/> is false.
    /// </summary>
    public OnnxEmbedder? Embedder { get; private set; }

    /// <summary>True when the ONNX model was found and loaded successfully.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Human-readable reason for skipping when <see cref="IsAvailable"/> is false.</summary>
    public string SkipReason { get; private set; } = string.Empty;

    /// <summary>
    /// Sink for routing log messages to the currently-running test's xUnit output.
    /// Each test-class constructor should call <c>Sink.SetOutput(output)</c> and
    /// each Dispose should call <c>Sink.SetOutput(null)</c>.
    /// </summary>
    public readonly XunitLogSink Sink = new();

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        // Check availability before triggering the lazy load.
        if (!SharedOnnxModel.IsAvailable)
        {
            IsAvailable = false;
            SkipReason  = $"ONNX model not found in {SharedOnnxModel.ModelDir}";
            // Output goes to Console here because no ITestOutputHelper is active yet.
            Console.WriteLine($"[SharedOnnxFixture] SKIP — {SkipReason}");
            return;
        }

        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[SharedOnnxFixture] Acquiring shared ONNX model " +
                          $"from {SharedOnnxModel.ModelDir} …");

        // SharedOnnxModel.Instance is a Lazy<OnnxEmbedder> — first access loads
        // and warms up the model; subsequent accesses return instantly.
        Embedder = SharedOnnxModel.Instance;

        Console.WriteLine($"[SharedOnnxFixture] Ready (dims={Embedder.Dimensions}, " +
                          $"elapsed={sw.Elapsed.TotalSeconds:F1}s)");
        IsAvailable = true;

        await Task.CompletedTask;   // satisfy IAsyncLifetime interface
    }

    public Task DisposeAsync()
    {
        // The OnnxEmbedder is owned by SharedOnnxModel (static) and lives for
        // the whole process.  We do NOT dispose it here.
        return Task.CompletedTask;
    }
}
