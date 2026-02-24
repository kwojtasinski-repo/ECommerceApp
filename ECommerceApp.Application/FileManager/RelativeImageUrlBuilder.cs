using ECommerceApp.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ECommerceApp.Application.FileManager
{
    internal sealed class RelativeImageUrlBuilder : IImageUrlBuilder
    {
        private readonly string _basePath;

        public RelativeImageUrlBuilder(IConfiguration configuration)
        {
            _basePath = configuration["Images:BasePath"] ?? "/api/images";
        }

        public string Build(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;
            return $"{_basePath}/{fileName}";
        }
    }
}
