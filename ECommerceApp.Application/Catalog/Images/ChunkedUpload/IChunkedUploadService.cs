using System;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    public interface IChunkedUploadService
    {
        /// <summary>Opens a session bound to <paramref name="userId"/>. Throws BusinessException on invalid input.</summary>
        Task<InitUploadResponse> InitUploadAsync(string userId, InitUploadRequest request);

        /// <summary>
        /// Stores one chunk. Returns null when the session does not exist or belongs to another user
        /// (the two cases are deliberately indistinguishable). Throws BusinessException on invalid chunks.
        /// </summary>
        Task<UploadChunkResponse> UploadChunkAsync(string userId, UploadChunkRequest request);

        /// <summary>Returns null when the session does not exist or belongs to another user.</summary>
        UploadStatusResponse GetStatus(string userId, Guid sessionId);
    }
}
