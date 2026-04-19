using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Catalog.Images.Upload;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Authorize(Roles = MaintenanceRole)]
    public class ImageController : BaseController
    {
        private readonly IImageService _service;
        private readonly IChunkedUploadService _chunkedUpload;

        public ImageController(IImageService service, IChunkedUploadService chunkedUpload)
        {
            _service = service;
            _chunkedUpload = chunkedUpload;
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
    }
}
