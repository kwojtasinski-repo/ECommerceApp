using ECommerceApp.Application.FileManager;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Interfaces
{
    public interface IFileStoreProvider
    {
        Task<FileDirectoryPOCO> WriteFileAsync(IFormFile file, string provider);
        byte[] ReadFile(string path, string provider);
        string GetFileExtenstion(string fileName, string provider);
    }
}
