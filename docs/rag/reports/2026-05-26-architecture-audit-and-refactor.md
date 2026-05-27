# RAG MCP — Architecture Audit & Refactor Report

**Date:** 2026-05-26
**Branch:** `RAG_Improvement`
**Scope:** .NET + Python MCP servers under `tools/rag-dotnet/` and `tools/rag/`.
**Status:** Refactor + global exception handlers implemented and validated (478 .NET + 204 Python tests green). Report uncommitted.

---

## 1. Inputs from the user

| # | Ask | Status |
|---|---|---|
| 1 | Can input clamps (`question` length, `top_k`, `top_files`) be expressed via attributes, .NET-style? | Investigated — see §2 |
| 2 | Is there a global exception handler for controllers and MCP so we don't leak stack traces? | Audited — see §3 |
| 3 | Refactor `app.Use(...)` inline lambda in `Program.cs` into a class-based middleware. | Done — see §4 |
| 4 | Refactor the duplicated `try { ... } catch (McpException) ... catch (Exception ex) ...` blocks into MCP-only middleware. | Done (helper, not ASP.NET middleware — see §5 for why) |
| 5 | Audit `AddOnnxEmbedderPipeline` (.NET) + Python equivalent. | Done — see §6 |

---

## 2. Attribute-based input validation in the MCP SDK

### Question
Can we replace inline `Math.Clamp(top_k, 1, MaxTopK)` and the 4096-char question cap with `[Range]` / `[StringLength]` attributes on `[McpServerTool]` parameters?

### Finding
**No, not directly with `ModelContextProtocol` 1.3.0.**

- The SDK reflects on parameters of `[McpServerTool]` methods to derive the JSON schema sent to the client (it reads `[Description]` and the declared CLR type). It does **not** evaluate `System.ComponentModel.DataAnnotations` validation attributes during argument binding — there is no `ValidationContext.Validate(...)` step in the SDK's reflection invoker.
- `AddCallToolFilter(...)` only wraps the *fallback* tool handler (the dictionary-based one). It does **not** wrap `[McpServerTool]`-attributed methods; those are invoked directly through the SDK's reflective `McpServerTool` wrapper.

### Options considered
| Option | Verdict |
|---|---|
| (A) Add `[Range(1, 20)]` / `[StringLength(4096)]` and hope the SDK validates. | Rejected — silently ignored by SDK 1.3.0. |
| (B) Write a custom `[McpClamp(min, max)]` / `[McpMaxLength(n)]` attribute + a reflective pre-invocation interceptor. | Possible but requires forking the SDK invoker or wrapping every `[McpServerTool]` method in a generated proxy. Over-engineered for 2 numeric clamps + 1 string cap. |
| (C) Keep the clamps inline, but centralise the string cap in a single private helper (`CapQuestion`) and the numeric caps as `public const int MaxTopK` next to their service. | **Chosen** — kept; one line per call, zero reflection cost, no SDK dependency. |

### Resulting shape in `RagTools.cs`
```csharp
private const int MaxQuestionChars = 4096;
// ...
top_k    = Math.Clamp(top_k,    1, RagQueryService.MaxTopK);
question = CapQuestion(question);
```
The bounds (`RagQueryService.MaxTopK`, `RagReadDocsService.MaxTopFiles`) already live next to the service that enforces them, which keeps the contract a one-liner per tool.

### Recommendation
Revisit if/when the SDK exposes a real per-invocation filter for attributed tools, or before adding a 4th/5th clamp — at that point a small custom attribute + a single reflective pre-invoker becomes worth it.

---

## 3. Global exception-handling audit

### .NET (`tools/rag-dotnet/src/RagTools.Mcp/Program.cs`)
| Layer | Before | After |
|---|---|---|
| ASP.NET controllers (`/ingest`, `/admin`) | None — exceptions propagated to Kestrel's default 5xx (HTML page, leaked stack in dev). | **`ApiExceptionHandler : IExceptionHandler`** registered via `AddExceptionHandler<ApiExceptionHandler>() + AddProblemDetails()` and engaged with `app.UseExceptionHandler()`. Maps `BadHttpRequestException`/`ArgumentException`→400, `UnauthorizedAccessException`→401, `NotImplementedException`→501, default→500. Sanitises payload via `ToolErrorSanitizer.Sanitize(exception)`. `OperationCanceledException` is passed through (returns `false` so the framework finalises). |
| Malformed body / JSON parsing | Inline `app.Use(async (ctx,next)=>...)` lambda with two `catch` arms in `Program.cs`. | **Extracted** to `Middleware/BadRequestEnvelopeMiddleware.cs` (sealed class, `InvokeAsync(HttpContext)` pattern, registered via `app.UseMiddleware<BadRequestEnvelopeMiddleware>()`). |
| MCP tool methods (4× `[McpServerTool]`) | 8-line `try { ... } catch (McpException) ... catch (OperationCanceledException) ... catch (Exception ex) { logger.LogError(...); throw ToolErrorSanitizer.ToMcpException(ex); }` repeated in every tool. | **Extracted** to `Tools/McpToolGuard.RunAsync<T>(...)` — see §5. |

**Pipeline order (HTTP branch):** `UseExceptionHandler` → `UseMiddleware<BadRequestEnvelopeMiddleware>` → `UseMiddleware<ApiKeyMiddleware>` → `MapControllers` → `MapMcp("/")`.

### Python (`tools/rag/mcp_server.py`)
- The MCP server is mounted on **Starlette**. **`_install_exception_handlers(app)`** (`mcp_server.py:149`) now registers two handlers, called from both `_run_sse` and `_run_http`:
  - `HTTPException` → `{"error": _sanitize_error_message(exc), "code": "HttpError"}`, original status preserved.
  - `Exception` → `{"error": _sanitize_error_message(exc), "code": "InternalServerError"}`, status 500.
- The existing in-tool guard at `call_tool` (`mcp_server.py:152-160`) is unchanged — still wraps every tool dispatch with `_sanitize_error_message`.
- Payload shape is identical to .NET (`{"error":"<sanitised>","code":"<bucket>"}`).

### Net assessment
| Concern | .NET | Python |
|---|---|---|
| MCP tool errors never leak stack to client | ✅ via `McpToolGuard` + `ToolErrorSanitizer` | ✅ via `call_tool` + `_sanitize_error_message` |
| Malformed HTTP body / JSON returns clean 400 envelope | ✅ via `BadRequestEnvelopeMiddleware` | ✅ via Starlette `HTTPException` handler |
| Controller / unhandled REST exception sanitised | ✅ via `ApiExceptionHandler` (`IExceptionHandler`) | ✅ via generic Starlette `Exception` handler |
| Envelope shape symmetric across stacks | ✅ `{error, code}` buckets: `BadRequest`/`Unauthorized`/`NotImplemented`/`HttpError`/`InternalServerError` | ✅ same |

---

## 4. `Program.cs` middleware refactor — before / after

**Before** (`Program.cs`, lines 149–171):
```csharp
app.UseMiddleware<ApiKeyMiddleware>();
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex) { /* 8 lines */ }
    catch (System.Text.Json.JsonException) { /* 6 lines */ }
});
app.MapControllers();
```

**After** (`Program.cs`, 3 lines):
```csharp
app.UseMiddleware<BadRequestEnvelopeMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers();
```

**New file:** [tools/rag-dotnet/src/RagTools.Mcp/Middleware/BadRequestEnvelopeMiddleware.cs](tools/rag-dotnet/src/RagTools.Mcp/Middleware/BadRequestEnvelopeMiddleware.cs) — same shape and conventions as the existing [ApiKeyMiddleware](tools/rag-dotnet/src/RagTools.Mcp/Middleware/ApiKeyMiddleware.cs). Registered first so it sees the most exceptions (including from API-key path).

---

## 5. MCP tool try/catch refactor — design + before / after

### Why not a real ASP.NET middleware?
The MCP layer is mounted via `app.MapMcp("/")`. ASP.NET middleware sits *outside* the tool dispatcher — it sees the HTTP POST to `/` but never the individual `[McpServerTool]` invocation. By the time control returns to the middleware, the MCP SDK has already serialized the tool result (or error) into the JSON-RPC response body and returned 200. Wrapping at the ASP.NET layer cannot intercept per-tool exceptions in a meaningful way.

### Why not a `CallToolFilter`?
As established in §2, the SDK 1.3.0 `AddCallToolFilter` only wraps the fallback handler, not attributed tools. Confirmed by reading the SDK reference and by the fact that simply registering a filter today has no effect on `RagTools.cs`.

### Chosen approach
A tiny static helper `McpToolGuard.RunAsync<T>(logger, toolName, body, ct)` in the same `Tools/` namespace. Each `[McpServerTool]` method becomes one call to the guard:

**Before** (per tool, ×4):
```csharp
try
{
    var request = new XRequest(...);
    var outcome = await xService.AsyncMethod(request, cancellationToken);
    return McpJson.Serialize(RagToolsProjector.ProjectX(outcome));
}
catch (McpException)              { throw; }
catch (OperationCanceledException) { throw; }
catch (Exception ex)
{
    logger.LogError(ex, "X failed");
    throw ToolErrorSanitizer.ToMcpException(ex);
}
```

**After** (per tool, ×4):
```csharp
return McpToolGuard.RunAsync(logger, nameof(X), async ct =>
{
    var request = new XRequest(...);
    var outcome = await xService.AsyncMethod(request, ct);
    return McpJson.Serialize(RagToolsProjector.ProjectX(outcome));
}, cancellationToken);
```

Boilerplate dropped: 32 lines → 12 lines across the four tools, single source of truth for the error envelope, **scoped to MCP only** (called only from `[McpServerTool]` methods — never reached by controllers).

**New file:** [tools/rag-dotnet/src/RagTools.Mcp/Tools/McpToolGuard.cs](tools/rag-dotnet/src/RagTools.Mcp/Tools/McpToolGuard.cs)
**Updated:** [tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs](tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs)

---

## 6. ONNX embedder pipeline audit

### .NET (`tools/rag-dotnet/src/RagTools.Core/`)
Pipeline = builder + pluggable pre/post processors + `IEmbedder` singleton.

**Composition root** (`Program.cs:111` and `:196`):
```csharp
webBuilder.Services.AddOnnxEmbedderPipeline(modelDir).Register();
```

**Layers**
1. `EmbedderServiceExtensions.AddOnnxEmbedderPipeline(IServiceCollection, modelDir)` →
   `new EmbedderPipelineBuilder(services).UseFactory(_ => OnnxEmbedder.Load(modelDir))`
   then `.WithPreprocessor<GlossaryExpansionPreprocessor>().WithPreprocessor<LengthTruncationPreprocessor>()`.
2. `EmbedderPipelineBuilder.Register()` — terminal call. Wires:
   - Each preprocessor type as `AddSingleton<IEmbedderPreprocessor, T>()` (order preserved by `GetServices<>`).
   - Each postprocessor type same way.
   - Inner embedder under a private marker record `InnerEmbedderMarker` (so it doesn't collide with the public `IEmbedder` slot).
   - `IEmbedder` singleton = `new PipelinedEmbedder(inner, preprocessors, postprocessors)`.

**Strengths**
- Single composition root; tests inject a fake `IEmbedder` directly without touching the builder.
- Order of preprocessors guaranteed by registration order (relies on DI's `GetServices<T>` ordering, which is documented as registration order for transient/singleton chains).
- Builder is internal-constructor only; callers can't accidentally instantiate without the `AddOnnxEmbedderPipeline` / `AddOllamaEmbedderPipeline` entrypoints.
- Inner factory is lazy — model loads only when DI first resolves `IEmbedder`.
- Same builder reused for the Ollama backend → drop-in replacement.

**Findings / nits**
- `InnerEmbedderMarker` is a `sealed record` with a single `IEmbedder Value` — fine, but a private `sealed class { public IEmbedder Value { get; } }` would avoid the record-equality codegen that's never used here. Cosmetic only.
- `Register()` returns `IServiceCollection` for chaining, but `Program.cs` discards the return value — consider documenting that callers may chain extra `AddSingleton` calls.
- No telemetry / metric counters around the pipeline. If embedding latency becomes a concern, instrument `PipelinedEmbedder` (not the inner embedder) so pre/post overhead is visible.

### Python (`tools/rag/`)
Mirror of the .NET design, using duck-typed `Protocol`s instead of interfaces.

**Composition root** = `make_embedder(cfg)` in `tools/rag/make_embedder.py`:
```python
inner = SentenceTransformerEmbedder(cfg.embedder_model, device=cfg.embedder_device)
glossary = _load_multilingual_glossary(cfg.glossary_path)
return PipelinedEmbedder(
    inner,
    preprocessors=[
        GlossaryExpansionPreprocessor(glossary),
        LengthTruncationPreprocessor(max_words=512),
    ],
)
```

**Layers**
1. `Embedder` `Protocol` (runtime-checkable) — `dimensions`, `embed`, `embed_batch`.
2. `EmbedderPreprocessor` / `EmbedderPostprocessor` `Protocol`s — single `process(text|vector, ctx)` method.
3. `PipelinedEmbedder` — owns ordered lists of pre/post and dispatches.

**Symmetry vs .NET** ✅
| Aspect | .NET | Python |
|---|---|---|
| Pipeline class | `PipelinedEmbedder` | `PipelinedEmbedder` |
| Pre/post contract | `IEmbedderPreprocessor` interface | `EmbedderPreprocessor` Protocol |
| Default pre stack | Glossary → LengthTruncation | Glossary → LengthTruncation |
| `embed` default ctx | `QUERY_CTX` | `QUERY_CTX` |
| `embed_batch` default ctx | `INGEST_CTX` | `INGEST_CTX` |
| Inner-embedder swap | `UseFactory(...)` + Ollama variant | constructor injection (free choice) |
| Composition root | `AddOnnxEmbedderPipeline(...).Register()` | `make_embedder(cfg)` |

**Asymmetries / observations**
- .NET wires through DI; Python through a single factory function. Both are idiomatic.
- Python has no equivalent of `AddOllamaEmbedderPipeline` — `make_embedder` always builds a `SentenceTransformerEmbedder`. If Python ever needs to swap backends at runtime, lifting the inner-embedder choice into config is a small change.
- **Words vs tokens — verified symmetric.** Both stacks truncate by words. .NET `LengthTruncationPreprocessor` uses `text.Split(' ', RemoveEmptyEntries)` — its class doc states *"Uses a simple word split as a token proxy (avoids a SentencePiece dependency here)."* Python `LengthTruncationPreprocessor` uses `text.split()` capped at `max_words=512`. The only asymmetry is *where the limit comes from*: .NET reads `cfg.Chunker.MaxTokens`, Python is hardcoded `512` in `make_embedder.py`. Behaviour is identical today; the docstring in `EmbedderServiceExtensions.cs` was updated to call this out explicitly.
- Both implementations cover the pipeline well (28 chunks per pipeline test file, all green: 478 .NET / 204 Python).

### Verdict
Both pipelines are well-factored — single composition root, clear extension points, no leaky abstractions, no hidden globals, symmetric default stack. **No structural changes recommended.** Only nit: `InnerEmbedderMarker` `record` → `class` (cosmetic).

---

## 7. Tests

| Suite | Result |
|---|---|
| `.NET — dotnet test RagTools.sln -c Release` | **478 passed, 0 failed, 0 skipped** (after refactor) |
| `Python — pytest tools/rag/tests/ --ignore=tests/test_e2e.py` | **204 passed** (1.76 s) |

---

## 8. Files committed in this work block

Committed on `RAG_Improvement`:
| Commit | Title |
|---|---|
| `d1518e9e` | refactor(rag): extract MCP middleware + tool guard, add audit scripts |
| `5571b10d` | feat(rag): global API exception handlers for .NET and Python |
| `d8b80d05` | docs(rag): document error handlers, middleware stack, and JSON error envelope |
| `0dffa3ce` | docs(rag): add JSON error envelope rule to copilot-instructions and pipeline report |

Key files (now landed):
| Path | Role |
|---|---|
| `tools/rag-dotnet/src/RagTools.Mcp/Middleware/BadRequestEnvelopeMiddleware.cs` | Class-based malformed-request envelope middleware |
| `tools/rag-dotnet/src/RagTools.Mcp/Middleware/ApiExceptionHandler.cs` | Global `IExceptionHandler` — sanitised JSON envelope for controllers |
| `tools/rag-dotnet/src/RagTools.Mcp/Tools/McpToolGuard.cs` | Centralised MCP-only per-tool error envelope |
| `tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs` | All 4 tools delegate to `McpToolGuard.RunAsync` |
| `tools/rag-dotnet/src/RagTools.Mcp/Program.cs` | Pipeline = `UseExceptionHandler` → `BadRequestEnvelopeMiddleware` → `ApiKeyMiddleware` → controllers → MCP |
| `tools/rag/mcp_server.py` | `_install_exception_handlers(app)` wired into both `_run_sse` and `_run_http` |
| `tools/rag-dotnet/src/RagTools.Core/EmbedderServiceExtensions.cs` | Docstring corrected: truncates by **words** as a token proxy, symmetric with Python |
| `docs/rag/reports/2026-05-26-architecture-audit-and-refactor.md` | This report (uncommitted per policy) |

---

## 9. Follow-up suggestions (not done in this turn)

1. Add latency counters around `PipelinedEmbedder.embed{,_batch}` in both stacks.
2. Re-evaluate attribute-based validation once `ModelContextProtocol` ships a real per-invocation filter for `[McpServerTool]` methods (today only an inline `CapQuestion` + `Math.Clamp` is feasible).
3. Optional: lift Python's hardcoded `max_words=512` into config (3-line change in `make_embedder.py`) so both stacks read the limit from the same source. Not required — behaviour identical today.
4. Optional: `InnerEmbedderMarker` `record` → `class` (cosmetic; avoids unused record-equality codegen).
