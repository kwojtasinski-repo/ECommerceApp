﻿using ECommerceApp.Application.Exceptions;
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
        public byte[] ReadFileAsync(string path)
        {
            var bytes = File.ReadAllBytes(path);
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