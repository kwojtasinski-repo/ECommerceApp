using ECommerceApp.Application.POCO;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IFileStore
    {
        string SafeWriteFile(byte[] content, string sourceFileName, string path);
        ICollection<FileDirectoryPOCO> WriteFiles(ICollection<IFormFile> files, string path);
        FileDirectoryPOCO WriteFile(IFormFile file, string path);
        byte[] ReadFile(string path);
        string GetFileExtenstion(string file);
        string ReplaceInvalidChars(string fileName);
        byte[] GetFilesPackedIntoZip(ICollection<FileDirectoryPOCO> fileDirectories);
    }
}
