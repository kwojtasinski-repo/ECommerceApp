# Roadmap: Chunked Image Upload

> Status: 🔵 Planned — v1 spike not started
> Scope: `Web` project only (Catalog Image upload flow)
> TUS upgrade path: v2

---

## Problem

Current image upload (`ImageController.Add`) sends the full file in a single POST.
Large files fail silently on slow connections and give no progress feedback.

---

## Flow — Server-Driven (agreed design)

The server owns the chunking math. The client just follows instructions.

```
1. Client reads { fileName, fileSizeBytes, itemId? }
          │
          ▼
   POST /Catalog/Image/InitUpload
   body: { fileName, fileSizeBytes, itemId? }
          │
          ▼ Server responds:
   {
     sessionId:   GUID,
     chunkSize:   1_048_576,   ← server decides (1 MB default)
     totalChunks: N,
     chunkIds:    [1, 2, ..., N]  ← server assigns IDs
   }
          │
          ▼ Client sends each chunkId in order:
   POST /Catalog/Image/UploadChunk
   body: { sessionId, chunkId, chunk: IFormFile }
          │
          ├─ server writes Upload/Temp/{sessionId}/{chunkId}.part
          ├─ marks chunkId received in UploadSession
          ├─ returns { complete: false, receivedCount: N }
          │
          └─ when all chunkIds received:
                reassemble → validate (ext + total size) → _service.AddImages
                delete Upload/Temp/{sessionId}/
                return { complete: true, imageId: int }
          │
          ▼ JS:
   progress = receivedCount / totalChunks * 100 %
   on complete → show thumbnail, store imageId in hidden input
```

**Why server-driven**: zero math in JS, server controls chunk-size strategy,
`receivedCount` gives server-side observability per upload session.

---

## V1 — Spike (in-memory, filesystem)

### What to build

| Layer | File | Action |
|---|---|---|
| Controller | `Web/Areas/Catalog/Controllers/ImageController.cs` | Add `POST InitUpload` + `POST UploadChunk` actions |
| DTO | `Web/Areas/Catalog/Models/ChunkUploadDtos.cs` | `InitUploadRequest`, `InitUploadResponse`, `UploadChunkRequest` |
| Session store | `Web/Areas/Catalog/Upload/UploadSessionStore.cs` | `ConcurrentDictionary<Guid, UploadSession>` — registered as `Singleton` |
| Temp storage | `Upload/Temp/{sessionId}/{chunkId}.part` | Plain filesystem, created by controller |
| View | `Web/Areas/Catalog/Views/Product/AddItemNew.cshtml` | Copy of `Create.cshtml`, image section replaced with chunk upload widget |
| View | `Web/Areas/Catalog/Views/Product/EditItemNew.cshtml` | Copy of `Edit.cshtml`, image section replaced with chunk upload widget |
| JS | Inline `<script>` in the two views above | `fetch` loop — no AMD module yet |

### Session model

```csharp
internal sealed class UploadSession
{
    public Guid SessionId { get; init; }
    public string FileName { get; init; }
    public long FileSizeBytes { get; init; }
    public int? ItemId { get; init; }
    public int ChunkSize { get; init; }
    public IReadOnlyList<int> ChunkIds { get; init; }
    public HashSet<int> ReceivedChunkIds { get; } = new();
    public bool IsComplete => ReceivedChunkIds.SetEquals(ChunkIds);
}
```

### What we intentionally skip in v1

| Skip | Why |
|---|---|
| Resumable uploads (retry from last chunk) | Session-state complexity |
| Parallel chunk sending | Race conditions, ordering headache |
| DB session tracking | Filesystem is enough for a spike |
| Background cleanup job | Manual cleanup for now — `Upload/Temp/` wiped on app restart |
| TUS protocol | That is the v2 upgrade path |

### JS contract (inline, thin)

```js
// 1. init
const { sessionId, chunkSize, totalChunks, chunkIds } = await fetch('/Catalog/Image/InitUpload', {
    method: 'POST', body: JSON.stringify({ fileName, fileSizeBytes, itemId })
}).then(r => r.json());

// 2. send chunks
for (const chunkId of chunkIds) {
    const start = (chunkId - 1) * chunkSize;
    const slice = file.slice(start, start + chunkSize);
    const fd = new FormData();
    fd.append('sessionId', sessionId);
    fd.append('chunkId', chunkId);
    fd.append('chunk', slice, file.name);
    const res = await fetch('/Catalog/Image/UploadChunk', { method: 'POST', body: fd }).then(r => r.json());
    updateProgress(res.receivedCount / totalChunks);
    if (res.complete) { showThumbnail(res.imageId); break; }
}
```

---

## V2 — TUS upgrade path

| Step | What |
|---|---|
| 1 | Add `tusdotnet` NuGet to `Web` project |
| 2 | Register TUS middleware on `/tus-uploads` |
| 3 | Replace inline JS with `tus-js-client` via LibMan |
| 4 | Remove `InitUpload` + `UploadChunk` controller actions |
| 5 | Remove `UploadSessionStore` + `Upload/Temp/` logic |
| 6 | Resumable uploads come for free |

---

## Open questions before v1 start

- [ ] Where do `AddItemNew` / `EditItemNew` link from? Replace current Create/Edit or keep both?
- [ ] `UploadSessionStore` lifetime — `Singleton` is fine for v1, but needs expiry strategy before v2
- [ ] Max concurrent sessions per user — unlimited for v1?
