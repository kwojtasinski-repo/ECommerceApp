using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Interfaces
{
    public interface IFileWrapper
    {
        byte[] ReadFileAsync(string path);
        void WriteAllBytes(string outputFile, byte[] content);
        Task<string> WriteFileAsync(IFormFile file, string outputFile);
    }
}
