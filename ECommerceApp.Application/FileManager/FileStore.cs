using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.Application.FileManager
{
    public class FileStore : IFileStore
    {
        private readonly IFileWrapper _fileWrapper;
        private readonly IDirectoryWrapper _directoryWrapper;

        public FileStore(IFileWrapper fileWrapper, IDirectoryWrapper directoryWrapper)
        {
            _fileWrapper = fileWrapper;
            _directoryWrapper = directoryWrapper;
        }

        public string SafeWriteFile(byte[] content, string sourceFileName, string path)
        {
            _directoryWrapper.CreateDirectory(path);
            var outputFile = Path.Combine(path, sourceFileName);
            _fileWrapper.WriteAllBytes(outputFile, content);
            return outputFile;
        }

        public ICollection<FileDirectoryPOCO> WriteFiles(ICollection<IFormFile> files, string path)
        {
            _directoryWrapper.CreateDirectory(path);
            if (files != null && files.Count > 0)
            {
                var filesDirectory = new List<FileDirectoryPOCO>();
                foreach (IFormFile file in files)
                {
                    if (file != null)
                    {
                        string ext = Path.GetExtension(file.FileName);
                        var fileName = ReplaceInvalidChars(file.FileName);
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + fileName + ext;
                        var fileDirectory = new FileDirectoryPOCO() { SourcePath = path, Name = uniqueFileName };
                        var outputFile = Path.Combine(path, uniqueFileName);
                        _fileWrapper.WriteFileAsync(file, outputFile);
                        filesDirectory.Add(fileDirectory);
                    }
                }
                return filesDirectory;
            }
            else
            {
                throw new FileException("Files not found. List of files is empty");
            }
        }

        public FileDirectoryPOCO WriteFile(IFormFile file, string path)
        {
            _directoryWrapper.CreateDirectory(path);
            if (file != null)
            {
                string ext = GetFileExtenstion(file.FileName);
                var fileName = ReplaceInvalidChars(file.FileName);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + fileName + ext;
                var fileDirectory = new FileDirectoryPOCO() { SourcePath = path + Path.DirectorySeparatorChar + uniqueFileName, Name = fileName };
                var outputFile = Path.Combine(path, uniqueFileName);
                _fileWrapper.WriteFileAsync(file, outputFile);
                return fileDirectory;
            }
            else
            {
                throw new Exceptions.FileException("File not found");
            }
        }

        public byte[] ReadFile(string path)
        {
            var bytes = _fileWrapper.ReadFile(path);
            return bytes;
        }

        public string GetFileExtenstion(string file)
        {
            string ext = Path.GetExtension(file);
            return ext;
        }

        public string ReplaceInvalidChars(string fileName)
        {
            return string.Join("", fileName.Split(Path.GetInvalidFileNameChars()));
        }

        public byte[] GetFilesPackedIntoZip(ICollection<FileDirectoryPOCO> fileDirectories)
        {
            if (fileDirectories == null && fileDirectories.Count < 1)
            {
                throw new FileException("Invalid argument, empty collection");
            }

            byte[] zipBytes;

            using (var compressedFileStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(compressedFileStream, ZipArchiveMode.Create, true))
                {
                    foreach (var fileDirectory in fileDirectories)
                    {
                        //Create a zip entry for each attachment
                        // var bytes = ReadFile(fileDirectory.SourcePath);
                        var zipEntry = archive.CreateEntry(fileDirectory.Name);

                        //Get the stream of the attachment
                        using (var originalFileStream = new FileStream(fileDirectory.SourcePath, FileMode.OpenOrCreate))
                        using (var zipEntryStream = zipEntry.Open())
                        {
                            //Copy the attachment stream to the zip entry stream
                            originalFileStream.CopyTo(zipEntryStream);
                        }
                    }
                }
                zipBytes = compressedFileStream.ToArray();
            }
            return zipBytes;
        }

        public void DeleteFile(string path)
        {
            _fileWrapper.DeleteFile(path);
        }
    }
}
