using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.Application.FileManager
{
    public class FileWrapper : IFileWrapper
    {
        public void DeleteFile(string path)
        {
            var fileInfo = new FileInfo(path);

            if (fileInfo.Exists)
            {
                fileInfo.Delete();
            }
        }

        public byte[] ReadFile(string path)
        {
            var fileInfo = new FileInfo(path);

            if (fileInfo.Exists)
            {
                var bytes = File.ReadAllBytes(path);
                return bytes;
            }
            else
            {
                return Array.Empty<byte>();
            }
        }

        public void WriteAllBytes(string outputFile, byte[] content)
        {
            File.WriteAllBytes(outputFile, content);
        }

        public async Task<string> WriteFileAsync(IFormFile file, string outputFile)
        {
            try
            {
                using (FileStream fileStream = File.Create(outputFile))
                {
                    await file.CopyToAsync(fileStream);
                    fileStream.Flush();
                }
                return $"Uploaded successfully {outputFile}";
            }
            catch (Exception ex)
            {
                throw new SaveFileIssueException($"There was an issues with saving file {outputFile}", ex);
            }
        }
    }
}
