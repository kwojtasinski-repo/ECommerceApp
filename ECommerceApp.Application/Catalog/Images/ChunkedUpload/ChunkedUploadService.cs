using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    internal sealed class ChunkedUploadService : IChunkedUploadService
    {
        private const int DefaultChunkSize = 1_048_576;
        private static readonly string TempRoot = Path.Combine(Directory.GetCurrentDirectory(), "Upload", "Temp");

        private readonly UploadSessionStore _store;
        private readonly IImageService _imageService;

        public ChunkedUploadService(UploadSessionStore store, IImageService imageService)
        {
            _store = store;
            _imageService = imageService;
        }

        public InitUploadResponse InitUpload(InitUploadRequest request)
        {
            var totalChunks = (int)Math.Ceiling((double)request.FileSizeBytes / DefaultChunkSize);
            var chunkIds = Enumerable.Range(1, totalChunks).ToArray();

            var session = new UploadSession
            {
                SessionId = Guid.NewGuid(),
                FileName = request.FileName,
                FileSizeBytes = request.FileSizeBytes,
                ItemId = request.ItemId,
                ChunkSize = DefaultChunkSize,
                ChunkIds = chunkIds
            };
            _store.Create(session);

            return new InitUploadResponse
            {
                SessionId = session.SessionId,
                ChunkSize = DefaultChunkSize,
                TotalChunks = totalChunks,
                ChunkIds = chunkIds
            };
        }

        public async Task<UploadChunkResponse> UploadChunkAsync(UploadChunkRequest request)
        {
            if (!_store.TryGet(request.SessionId, out var session))
                throw new BusinessException("Upload session not found.");

            var tempDir = Path.Combine(TempRoot, request.SessionId.ToString());
            Directory.CreateDirectory(tempDir);
            var partPath = Path.Combine(tempDir, $"{request.ChunkId}.part");

            await using (var fs = File.Create(partPath))
                await request.Chunk.CopyToAsync(fs);

            lock (session.ReceivedChunkIds)
                session.ReceivedChunkIds.Add(request.ChunkId);

            if (!session.IsComplete)
            {
                return new UploadChunkResponse
                {
                    Complete = false,
                    ReceivedCount = session.ReceivedChunkIds.Count
                };
            }

            return await AssembleAndSaveAsync(session, tempDir);
        }

        private async Task<UploadChunkResponse> AssembleAndSaveAsync(UploadSession session, string tempDir)
        {
            var assembledPath = Path.Combine(tempDir, session.FileName);

            await using (var output = File.Create(assembledPath))
            {
                foreach (var chunkId in session.ChunkIds)
                {
                    await using var chunkFs = File.OpenRead(Path.Combine(tempDir, $"{chunkId}.part"));
                    await chunkFs.CopyToAsync(output);
                }
            }

            await using var assembledStream = File.OpenRead(assembledPath);
            IFormFile formFile = new FormFile(assembledStream, 0, assembledStream.Length, "files", session.FileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };

            try
            {
                await _imageService.AddImages(new AddImagesPOCO
                {
                    Files = new[] { formFile },
                    ItemId = session.ItemId
                });
            }
            finally
            {
                _store.Remove(session.SessionId);
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
            }

            return new UploadChunkResponse
            {
                Complete = true,
                ReceivedCount = session.ChunkIds.Count
            };
        }
    }
}
