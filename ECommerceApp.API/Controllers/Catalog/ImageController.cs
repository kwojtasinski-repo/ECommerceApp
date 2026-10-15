using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Catalog.Images.ChunkedUpload;
using ECommerceApp.Application.Catalog.Images.ViewModels;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.Catalog
{
    [Authorize]
    [Route("api/images")]
    public class ImageController : BaseController
    {
        private readonly IImageService _service;
        private readonly IUrlImageResolver _resolver;
        private readonly IChunkedUploadService _chunkedUpload;
        private readonly ILogger<ImageController> _logger;

        public ImageController(IImageService service, IUrlImageResolver resolver, IChunkedUploadService chunkedUpload, ILogger<ImageController> logger)
        {
            _service = service;
            _resolver = resolver;
            _chunkedUpload = chunkedUpload;
            _logger = logger;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public async Task<ActionResult<List<GetImageVm>>> GetAll()
        {
            var images = await _service.GetAll();
            return images;
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var result = await _resolver.ResolveAsync(id);
            if (result is null)
            {
                return NotFound();
            }

            return File(result.Bytes, result.ContentType, result.FileName);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public async Task<ActionResult<int>> AddImage([FromForm] AddImagePOCO image)
        {
            var imageVm = new ImageVm { Id = 0, Images = new List<IFormFile>() { image.File }, ItemId = image.ItemId };
            var id = await _service.Add(imageVm);
            _logger.LogInformation("Added image with id {Id} for item {ItemId}", id, image.ItemId);
            return id;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost("multi-upload")]
        public async Task<ActionResult<List<int>>> AddImages([FromForm] AddImagesPOCO images)
        {
            var ids = await _service.AddImages(images);
            return ids;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost("init-upload")]
        public async Task<IActionResult> InitUpload([FromBody] InitUploadRequest request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request.");
            }

            try
            {
                return Ok(await _chunkedUpload.InitUploadAsync(GetUserId(), request));
            }
            catch (Application.Exceptions.BusinessException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost("upload-chunk")]
        [RequestSizeLimit(ChunkedUploadLimits.MaxChunkRequestBodyBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = ChunkedUploadLimits.MaxChunkRequestBodyBytes)]
        public async Task<IActionResult> UploadChunk([FromForm] UploadChunkRequest request)
        {
            if (request?.Chunk == null)
            {
                return BadRequest("Missing chunk.");
            }

            try
            {
                var response = await _chunkedUpload.UploadChunkAsync(GetUserId(), request);
                return response == null ? NotFound() : Ok(response);
            }
            catch (Application.Exceptions.BusinessException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet("upload-status")]
        public IActionResult UploadStatus([FromQuery] Guid sessionId)
        {
            var status = _chunkedUpload.GetStatus(GetUserId(), sessionId);
            return status == null ? NotFound() : Ok(status);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImage(int id)
        {
            return await _service.Delete(id)
                ? Ok()
                : NotFound();
        }
    }
}
