using ECommerceApp.Application.POCO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Image;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/images")]
    public class ImageController : BaseController
    {
        private readonly IImageService _service;

        public ImageController(IImageService service)
        {
            _service = service;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public ActionResult<List<GetImageVm>> GetAll()
        {
            var images = _service.GetAll();
            return images;
        }

        [HttpGet("{id}")]
        public ActionResult<GetImageVm> Get(int id)
        {
            var image = _service.Get(id);
            return image;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public ActionResult<int> AddImage([FromForm] AddImagePOCO image)
        {
            var imageVm = new ImageVm { Id = 0, Images = new List<IFormFile>() { image.File }, ItemId = image.ItemId };
            var id = _service.Add(imageVm);
            return id;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost("multi-upload")]
        public ActionResult<List<int>> AddImages([FromForm] AddImagesPOCO images)
        {
            var ids = _service.AddImages(images);
            return ids;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpDelete("{id}")]
        public IActionResult DeleteImage(int id)
        {
            _service.Delete(id);
            return Ok();
        }
    }
}
