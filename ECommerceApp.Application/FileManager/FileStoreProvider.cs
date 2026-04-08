using ECommerceApp.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ECommerceApp.Application.FileManager
{
    internal sealed class FileStoreProvider : IFileStoreProvider
    {
        private readonly IFileStore _localStore;

        public FileStoreProvider(IFileStore localStore)
        {
            _localStore = localStore;
        }

        public Task<FileDirectoryPOCO> WriteFileAsync(IFormFile file, string provider)
            => GetStore(provider).WriteFileAsync(file);

        public byte[] ReadFile(string path, string provider)
            => GetStore(provider).ReadFile(path);

        public string GetFileExtenstion(string fileName, string provider)
            => GetStore(provider).GetFileExtenstion(fileName);

        private IFileStore GetStore(string provider) => _localStore;
    }
}
