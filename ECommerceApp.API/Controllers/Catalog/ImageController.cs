using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Catalog.Images.ViewModels;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        public ImageController(IImageService service, IUrlImageResolver resolver)
        {
            _service = service;
            _resolver = resolver;
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
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImage(int id)
        {
            return await _service.Delete(id)
                ? Ok()
                : NotFound();
        }
    }
}
