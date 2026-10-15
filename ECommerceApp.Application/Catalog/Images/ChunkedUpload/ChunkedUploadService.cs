using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.Catalog.Products;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    internal sealed class ChunkedUploadService : IChunkedUploadService
    {
        private const string AssembledFileName = "assembled.bin";

        private readonly UploadSessionStore _store;
        private readonly IImageService _imageService;
        private readonly IProductRepository _productRepository;
        private readonly ChunkedUploadOptions _options;

        public ChunkedUploadService(
            UploadSessionStore store,
            IImageService imageService,
            IProductRepository productRepository,
            IOptions<ChunkedUploadOptions> options)
        {
            _store = store;
            _imageService = imageService;
            _productRepository = productRepository;
            _options = options.Value;
        }

        public async Task<InitUploadResponse> InitUploadAsync(string userId, InitUploadRequest request)
        {
            if (string.IsNullOrWhiteSpace(userId) || request is null)
            {
                throw new BusinessException("Invalid upload request.");
            }

            var fileName = UploadFileName.Normalize(request.FileName);

            if (request.FileSizeBytes <= 0)
            {
                throw new BusinessException("File size must be greater than zero.", ErrorCode.Create("invalidFileSize"));
            }

            if (request.FileSizeBytes > ImageConstraints.MaxFileSizeBytes)
            {
                throw new BusinessException(
                    $"Image {fileName} is too big ({request.FileSizeBytes} bytes). Allowed {ImageConstraints.MaxFileSizeBytes} bytes",
                    ErrorCode.Create("fileSizeTooBig", new List<ErrorParameter>
                    {
                        ErrorParameter.Create("name", fileName),
                        ErrorParameter.Create("size", request.FileSizeBytes),
                        ErrorParameter.Create("allowedSize", ImageConstraints.MaxFileSizeBytes)
                    }));
            }

            if (request.ItemId.HasValue
                && (request.ItemId.Value <= 0 || !await _productRepository.ExistsByIdAsync(new ProductId(request.ItemId.Value))))
            {
                throw new BusinessException(
                    $"Product with id '{request.ItemId.Value}' was not found",
                    ErrorCode.Create("productNotFound", ErrorParameter.Create("id", request.ItemId.Value)));
            }

            var totalChunks = (int)((request.FileSizeBytes + ChunkedUploadLimits.ChunkSizeBytes - 1) / ChunkedUploadLimits.ChunkSizeBytes);
            var session = new UploadSession
            {
                SessionId = Guid.NewGuid(),
                UserId = userId,
                FileName = fileName,
                FileSizeBytes = request.FileSizeBytes,
                ItemId = request.ItemId,
                ChunkSize = ChunkedUploadLimits.ChunkSizeBytes,
                TotalChunks = totalChunks
            };

            if (!_store.TryCreate(session, _options.MaxOpenSessionsPerUser))
            {
                throw new BusinessException(
                    "Too many open upload sessions.",
                    ErrorCode.Create("tooManyUploadSessions", ErrorParameter.Create("allowed", _options.MaxOpenSessionsPerUser)));
            }

            return new InitUploadResponse
            {
                SessionId = session.SessionId,
                ChunkSize = session.ChunkSize,
                TotalChunks = totalChunks,
                ChunkIds = session.ChunkIds
            };
        }

        public UploadStatusResponse GetStatus(string userId, Guid sessionId)
        {
            if (!TryGetOwnedSession(userId, sessionId, out var session))
            {
                return null;
            }

            lock (session.SyncRoot)
            {
                return new UploadStatusResponse
                {
                    SessionId = session.SessionId,
                    ChunkSize = session.ChunkSize,
                    TotalChunks = session.TotalChunks,
                    ReceivedChunkIds = session.ReceivedChunkIds.OrderBy(i => i).ToArray(),
                    Complete = session.Completion != null || session.ReceivedChunkIds.Count == session.TotalChunks
                };
            }
        }

        public async Task<UploadChunkResponse> UploadChunkAsync(string userId, UploadChunkRequest request)
        {
            if (request is null || !TryGetOwnedSession(userId, request.SessionId, out var session))
            {
                return null;
            }

            if (request.ChunkId < 1 || request.ChunkId > session.TotalChunks)
            {
                throw new BusinessException("Chunk id is out of range.", ErrorCode.Create("invalidChunkId"));
            }

            if (request.Chunk is null)
            {
                throw new BusinessException("Missing chunk.", ErrorCode.Create("invalidChunk"));
            }

            var expectedLength = session.ExpectedChunkLength(request.ChunkId);
            if (request.Chunk.Length != expectedLength)
            {
                throw new BusinessException(
                    $"Chunk {request.ChunkId} must be exactly {expectedLength} bytes.",
                    ErrorCode.Create("invalidChunkSize", ErrorParameter.Create("expected", expectedLength)));
            }

            TaskCompletionSource<UploadChunkResponse> pending;
            lock (session.SyncRoot)
            {
                pending = session.Completion;
            }

            if (pending != null)
            {
                // The upload is already being assembled: re-sends are idempotent and share its outcome.
                return await pending.Task;
            }

            var tempPath = Path.Combine(_store.GetSessionDirectory(session.SessionId), $"{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await request.Chunk.CopyToAsync(fs);
                }
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }

            if (new FileInfo(tempPath).Length != expectedLength)
            {
                File.Delete(tempPath);
                throw new BusinessException("Chunk length does not match the declared size.", ErrorCode.Create("invalidChunkSize"));
            }

            TaskCompletionSource<UploadChunkResponse> completion = null;
            int receivedCount;
            lock (session.SyncRoot)
            {
                if (session.Completion != null)
                {
                    File.Delete(tempPath);
                    pending = session.Completion;
                }
                else
                {
                    File.Move(tempPath, _store.GetPartPath(session.SessionId, request.ChunkId), overwrite: true);
                    session.ReceivedChunkIds.Add(request.ChunkId);
                    receivedCount = session.ReceivedChunkIds.Count;
                    if (receivedCount == session.TotalChunks)
                    {
                        completion = session.Completion = new TaskCompletionSource<UploadChunkResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                    else
                    {
                        return new UploadChunkResponse { Complete = false, ReceivedCount = receivedCount };
                    }
                }
            }

            if (completion is null)
            {
                return await pending.Task;
            }

            try
            {
                completion.SetResult(await AssembleAndSaveAsync(session));
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }

            return await completion.Task;
        }

        private bool TryGetOwnedSession(string userId, Guid sessionId, out UploadSession session)
        {
            if (!string.IsNullOrEmpty(userId)
                && _store.TryGet(sessionId, out session)
                && string.Equals(session.UserId, userId, StringComparison.Ordinal))
            {
                return true;
            }

            session = null;
            return false;
        }

        private async Task<UploadChunkResponse> AssembleAndSaveAsync(UploadSession session)
        {
            // Rejected or not, the session and its temp files are gone afterwards.
            try
            {
                var sessionDir = _store.GetSessionDirectory(session.SessionId);
                // Server-generated name: the client file name is never used as a disk path.
                var assembledPath = Path.Combine(sessionDir, AssembledFileName);

                try
                {
                    await using var output = new FileStream(assembledPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    foreach (var chunkId in session.ChunkIds)
                    {
                        await using var chunk = File.OpenRead(_store.GetPartPath(session.SessionId, chunkId));
                        await chunk.CopyToAsync(output);
                    }
                }
                catch (IOException ex)
                {
                    throw new BusinessException("Upload data is incomplete.", ex);
                }

                if (new FileInfo(assembledPath).Length != session.FileSizeBytes)
                {
                    throw new BusinessException(
                        "Assembled file size does not match the declared size.",
                        ErrorCode.Create("invalidFileSize"));
                }

                List<int> imageIds;
                await using (var assembled = new FileStream(assembledPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    IFormFile formFile = new FormFile(assembled, 0, assembled.Length, "files", session.FileName)
                    {
                        Headers = new HeaderDictionary(),
                        ContentType = "application/octet-stream"
                    };

                    imageIds = await _imageService.AddImages(new AddImagesPOCO
                    {
                        Files = new[] { formFile },
                        ItemId = session.ItemId
                    });
                }

                return new UploadChunkResponse
                {
                    Complete = true,
                    ReceivedCount = session.TotalChunks,
                    ImageId = imageIds?.FirstOrDefault()
                };
            }
            finally
            {
                _store.Remove(session.SessionId);
            }
        }
    }
}
