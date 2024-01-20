using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Interfaces
{
    public interface IFileWrapper
    {
        byte[] ReadFile(string path);
        void WriteAllBytes(string outputFile, byte[] content);
        Task<string> WriteFileAsync(IFormFile file, string outputFile);
        void DeleteFile(string path);
        void WriteFileAsync(byte[] file, string outputFile);
    }
}
