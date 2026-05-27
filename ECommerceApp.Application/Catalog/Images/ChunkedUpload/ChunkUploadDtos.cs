using System;
using Microsoft.AspNetCore.Http;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    public sealed class InitUploadRequest
    {
        public string FileName { get; set; }
        public long FileSizeBytes { get; set; }
        public int? ItemId { get; set; }
    }

    public sealed class InitUploadResponse
    {
        public Guid SessionId { get; set; }
        public int ChunkSize { get; set; }
        public int TotalChunks { get; set; }
        public int[] ChunkIds { get; set; } = Array.Empty<int>();
    }

    public sealed class UploadChunkRequest
    {
        public Guid SessionId { get; set; }
        public int ChunkId { get; set; }
        public IFormFile Chunk { get; set; }
    }

    public sealed class UploadChunkResponse
    {
        public bool Complete { get; set; }
        public int ReceivedCount { get; set; }
        public int? ImageId { get; set; }
    }
}
