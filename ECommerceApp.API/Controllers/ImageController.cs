using ECommerceApp.Application.POCO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ECommerceApp.API.Controllers
{
    [Route("api/images")]
    [Authorize]
    [ApiController]
    public class ImageController : ControllerBase
    {
        private readonly IImageService _service;

        public ImageController(IImageService service)
        {
            _service = service;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public ActionResult<List<ImageVm>> GetAll()
        {
            var images = _service.GetAll();
            return images;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet("{id}")]
        public ActionResult<ImageVm> Get(int id)
        {
            var image = _service.Get(id);
            return image;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public ActionResult<int> AddImage([FromForm] AddImagePOCO image)
        {
            var imageVm = new ImageVm { Id = 0, Images = new List<IFormFile>() { image.File }, ItemId = image.ItemId };
            var id = _service.Add(imageVm);
            return id;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost("multi-upload")]
        public ActionResult<List<int>> AddImages([FromForm] AddImagesPOCO images)
        {
            var ids = _service.AddImages(images);
            return ids;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPatch("{id}")]
        public IActionResult PartialImageUpdate(int id, [FromBody] UpdateImagePOCO image)
        {
            image.Id = id;
            _service.PartialUpdate(image);
            return Ok();
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpDelete("{id}")]
        public IActionResult DeleteImage(int id)
        {
            _service.Delete(id);
            return Ok();
        }
    }
}
