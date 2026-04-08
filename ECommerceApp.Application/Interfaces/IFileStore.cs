using ECommerceApp.Application.FileManager;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Interfaces
{
    public interface IFileStore
    {
        string SafeWriteFile(byte[] content, string sourceFileName, string path);
        ICollection<FileDirectoryPOCO> WriteFiles(ICollection<IFormFile> files, string path);
        Task<FileDirectoryPOCO> WriteFileAsync(IFormFile file);
        Task<FileDirectoryPOCO> WriteFileAsync(IFormFile file, string path);
        FileDirectoryPOCO WriteFile(string fileName, byte[] file, string path);
        byte[] ReadFile(string path);
        Task<byte[]> ReadFileAsync(string path);
        string GetFileExtenstion(string file);
        string ReplaceInvalidChars(string fileName);
        byte[] GetFilesPackedIntoZip(ICollection<FileDirectoryPOCO> fileDirectories);
        void DeleteFile(string path);
    }
}
