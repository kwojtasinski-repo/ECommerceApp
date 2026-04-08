using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Catalog.Products;
using System.IO;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Images.Services
{
    internal sealed class UrlImageResolver : IUrlImageResolver
    {
        private readonly IImageRepository _imageRepository;
        private readonly IFileStoreProvider _fileStoreProvider;

        public UrlImageResolver(IImageRepository imageRepository, IFileStoreProvider fileStoreProvider)
        {
            _imageRepository = imageRepository;
            _fileStoreProvider = fileStoreProvider;
        }

        public async Task<ImageFileResult?> ResolveAsync(int imageId)
        {
            var image = await _imageRepository.GetImageById(imageId);
            if (image is null)
            {
                return null;
            }

            var bytes = _fileStoreProvider.ReadFile(image.FileSource, image.Provider);
            var ext = Path.GetExtension(image.FileName.Value).ToLower();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"            => "image/png",
                ".gif"            => "image/gif",
                ".webp"           => "image/webp",
                _                 => "application/octet-stream"
            };
            return new ImageFileResult(bytes, contentType, image.FileName.Value);
        }
    }
}
