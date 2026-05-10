using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Catalog.Images.Upload;
using ECommerceApp.Application.Catalog.Images.ViewModels;
using ECommerceApp.Web.Areas.Catalog.Options;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;

namespace ECommerceApp.Web.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Authorize(Roles = MaintenanceRole)]
    public class ImageController : BaseController
    {
        private readonly IImageService _service;
        private readonly IChunkedUploadService _chunkedUpload;
        private readonly ITusStore _tusStore;
        private readonly CatalogOptions _catalogOptions;

        public ImageController(
            IImageService service,
            IChunkedUploadService chunkedUpload,
            ITusStore tusStore,
            IOptions<CatalogOptions> catalogOptions)
        {
            _service = service;
            _chunkedUpload = chunkedUpload;
            _tusStore = tusStore;
            _catalogOptions = catalogOptions.Value;
        }

        [HttpPost]
        public async Task<ActionResult<List<int>>> UploadImages(int itemId, [FromForm] ICollection<IFormFile> files)
        {
            var addImages = new AddImagesPOCO { Files = files, ItemId = itemId };
            try
            {
                await _service.AddImages(addImages);
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
            return Ok();
        }

        [HttpPost]
        public IActionResult InitUpload([FromBody] InitUploadRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FileName) || request.FileSizeBytes <= 0)
                return BadRequest("Invalid request.");

            return Ok(_chunkedUpload.InitUpload(request));
        }

        [HttpPost]
        public async Task<IActionResult> UploadChunk([FromForm] UploadChunkRequest request)
        {
            if (request?.Chunk == null)
                return BadRequest("Missing chunk.");

            try
            {
                return Ok(await _chunkedUpload.UploadChunkAsync(request));
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }

        /// <summary>
        /// Bridge endpoint: called by the client after tus-js-client finishes all PATCH chunks.
        /// Reads the assembled bytes from the TUS store, wraps them in an IFormFile, and persists
        /// via IImageService — same path as the classic single-POST upload.
        ///
        /// Returns 200 + { imageId } on success.
        /// Returns 422 Unprocessable when the upload is not yet complete (offset &lt; length).
        /// Returns 404 when the TUS upload ID is not found.
        /// Returns 400 when TUS is not the active upload engine.
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CompleteUpload(
            [FromBody] CompleteUploadRequest request,
            CancellationToken cancellationToken)
        {
            if (!_catalogOptions.UseTusUpload)
                return BadRequest("TUS upload is not enabled.");

            if (request is null || string.IsNullOrWhiteSpace(request.TusUploadUrl) || request.ItemId <= 0)
                return BadRequest("Invalid request.");

            // Extract the upload ID from the trailing path segment of the TUS location URL.
            var fileId = request.TusUploadUrl.TrimEnd('/').Split('/')[^1];

            if (!await _tusStore.FileExistAsync(fileId, cancellationToken))
                return NotFound();

            var offset = await _tusStore.GetUploadOffsetAsync(fileId, cancellationToken);
            var length = await _tusStore.GetUploadLengthAsync(fileId, cancellationToken);

            if (offset < length)
                return UnprocessableEntity("Upload is not yet complete.");

            // Read assembled bytes from the TUS store.
            var tusReadable = (ITusReadableStore)_tusStore;
            var tusFile = await tusReadable.GetFileAsync(fileId, cancellationToken);
            var metadata = await tusFile.GetMetadataAsync(cancellationToken);

            var fileName = metadata.TryGetValue("filename", out var fn)
                ? fn.GetString(Encoding.UTF8)
                : "upload.bin";

            using var contentStream = await tusFile.GetContentAsync(cancellationToken);
            var byteCount = (int)length!.Value;
            var bytes = new byte[byteCount];
            await contentStream.ReadExactlyAsync(bytes, 0, byteCount, cancellationToken);

            using var ms = new MemoryStream(bytes);
            var formFile = new FormFile(ms, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = ResolveContentType(fileName)
            };

            var imageVm = new ImageVm
            {
                Id = 0,
                Images = new List<IFormFile> { formFile },
                ItemId = request.ItemId
            };

            try
            {
                var imageId = await _service.Add(imageVm);
                return Ok(new { imageId });
            }
            catch (BusinessException ex)
            {
                return BadRequest(BuildErrorModel(ex).Codes);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteImage(int id)
        {
            try
            {
                return await _service.Delete(id)
                    ? Ok()
                    : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }

        private static string ResolveContentType(string fileName) =>
            Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
    }

    /// <summary>Request body for <c>POST /Catalog/Image/CompleteUpload</c>.</summary>
    public sealed record CompleteUploadRequest(string TusUploadUrl, int ItemId);
}
