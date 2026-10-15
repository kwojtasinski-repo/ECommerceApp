using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    internal sealed class UploadSession
    {
        public Guid SessionId { get; init; }
        public string UserId { get; init; }
        /// <summary>Sanitized display name — never a disk path.</summary>
        public string FileName { get; init; }
        public long FileSizeBytes { get; init; }
        public int? ItemId { get; init; }
        public int ChunkSize { get; init; }
        public int TotalChunks { get; init; }
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        /// <summary>Guards <see cref="ReceivedChunkIds"/> and <see cref="Completion"/>.</summary>
        public object SyncRoot { get; } = new();
        public HashSet<int> ReceivedChunkIds { get; } = new();

        /// <summary>Set (under <see cref="SyncRoot"/>) by the single request that wins the right to assemble.</summary>
        public TaskCompletionSource<UploadChunkResponse> Completion { get; set; }

        public int[] ChunkIds => Enumerable.Range(1, TotalChunks).ToArray();

        public int ExpectedChunkLength(int chunkId)
            => chunkId < TotalChunks
                ? ChunkSize
                : (int)(FileSizeBytes - (long)(TotalChunks - 1) * ChunkSize);
    }
}
