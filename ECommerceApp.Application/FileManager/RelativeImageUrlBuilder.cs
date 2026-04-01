using ECommerceApp.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ECommerceApp.Application.FileManager
{
    internal sealed class RelativeImageUrlBuilder : IImageUrlBuilder
    {
        private readonly string _basePath;
        private readonly string _baseUrl;
        private readonly string _segment;

        public RelativeImageUrlBuilder(IConfiguration configuration)
        {
            _basePath = configuration["Images:BasePath"] ?? string.Empty;
            _baseUrl  = configuration["Images:BaseUrl"]  ?? string.Empty;
            // Derive the canonical segment from the last part of the path: "/api/images" → "images"
            _segment  = _basePath.TrimStart('/').Contains('/')
                ? _basePath.TrimStart('/').Substring(_basePath.TrimStart('/').LastIndexOf('/') + 1)
                : _basePath.TrimStart('/');
        }

        /// <inheritdoc/>
        public string Build(int imageId)
        {
            if (imageId <= 0)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(_baseUrl)
                ? $"{_basePath}/{imageId}"
                : $"{_baseUrl}{_basePath}/{imageId}";
        }

        /// <inheritdoc/>
        public string GetCanonical(int imageId)
        {
            if (imageId <= 0)
            {
                return string.Empty;
            }

            return $"{_segment}/{imageId}";
        }
    }
}
