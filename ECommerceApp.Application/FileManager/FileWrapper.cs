using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.Application.FileManager
{
    public class FileWrapper : IFileWrapper
    {
        private readonly object _fileLock = new object();

        public void DeleteFile(string path)
        {
            var fileInfo = new FileInfo(path);

            try
            {
                if (fileInfo.Exists)
                {
                    fileInfo.Delete();
                }
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
            }
            catch (Exception)
            {
                throw;
            }
        }

        public byte[] ReadFile(string path)
        {
            var fileInfo = new FileInfo(path);
            var bytes = Array.Empty<byte>();
                
            try
            {
                if (fileInfo.Exists)
                {
                    bytes = File.ReadAllBytes(path);
                }
            }
            catch(IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return Array.Empty<byte>();
            }
            catch(Exception)
            {
                throw;
            }

            return bytes;
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
