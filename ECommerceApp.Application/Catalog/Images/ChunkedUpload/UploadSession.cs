using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
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
}
