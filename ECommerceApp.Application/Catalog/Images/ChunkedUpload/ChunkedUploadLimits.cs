namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    public static class ChunkedUploadLimits
    {
        /// <summary>Fixed size of every chunk except the last one.</summary>
        public const int ChunkSizeBytes = 1_048_576;

        /// <summary>Hard HTTP body limit for one chunk request: the chunk plus multipart/form overhead.</summary>
        public const int MaxChunkRequestBodyBytes = ChunkSizeBytes + 64 * 1024;
    }
}
