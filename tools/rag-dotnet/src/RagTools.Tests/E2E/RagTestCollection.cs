using Xunit;

namespace RagTools.Tests.E2E;

// ──────────────────────────────────────────────────────────────────────────────
// xUnit collection definition for all Rag E2E tests.
//
// Purpose:
//   1. Share SharedOnnxFixture (the ONNX model) across IngestE2ETests and
//      HttpIngestE2ETests — the model is loaded ONCE per test run.
//   2. Force sequential execution of all E2E tests within this collection —
//      eliminating CPU contention from parallel ONNX inference sessions.
//
// How to add a new E2E test class to this collection:
//   Decorate the class with:  [Collection(RagTestCollection.Name)]
//   Then add SharedOnnxFixture as a constructor parameter.
//
// Reference: https://xunit.net/docs/shared-context#collection-fixture
// ──────────────────────────────────────────────────────────────────────────────

[CollectionDefinition(RagTestCollection.Name)]
public sealed class RagTestCollection : ICollectionFixture<SharedOnnxFixture>
{
    /// <summary>Collection name constant — use in [Collection(...)] attributes.</summary>
    public const string Name = "Rag E2E";

    // This class has no code, and is never instantiated.
    // Its purpose is solely to be the target for [CollectionDefinition].
}
