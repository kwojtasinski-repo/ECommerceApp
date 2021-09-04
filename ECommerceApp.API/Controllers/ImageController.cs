using ECommerceApp.Application.POCO;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Image;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Route("api/images")]
    [ApiController]
    public class ImageController : ControllerBase
    {
        private readonly ImageServiceAbstract _service;

        public ImageController(ImageServiceAbstract service)
        {
            _service = service;
        }

        [HttpGet]
        public ActionResult<List<ImageVm>> GetAll()
        {
            var images = _service.GetAll();
            return images;
        }

        [HttpGet("{id}")]
        public ActionResult<ImageVm> Get(int id)
        {
            var image = _service.Get(id);
            return image;
        }

        [HttpPost]
        public ActionResult<int> AddImage([FromForm] AddImagePOCO image)
        {
            var imageVm = new ImageVm { Id = 0, Images = new List<IFormFile>() { image.File }, ItemId = image.ItemId };
            var id = _service.Add(imageVm);
            return id;
        }

        [HttpPost("multi-upload")]
        public ActionResult<List<int>> AddImages([FromForm] AddImagesPOCO images)
        {
            var ids = _service.AddImages(images);
            return ids;
        }

        [HttpPatch("{id}")]
        public IActionResult PartialImageUpdate(int id, [FromBody] UpdateImagePOCO image)
        {
            image.Id = id;
            _service.PartialUpdate(image);
            return Ok();
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteImage(int id)
        {
            _service.Delete(id);
            return Ok();
        }
    }
}
