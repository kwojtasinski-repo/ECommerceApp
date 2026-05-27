using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    public interface IChunkedUploadService
    {
        InitUploadResponse InitUpload(InitUploadRequest request);
        Task<UploadChunkResponse> UploadChunkAsync(UploadChunkRequest request);
    }
}
