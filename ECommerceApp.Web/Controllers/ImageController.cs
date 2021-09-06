using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.POCO;
using ECommerceApp.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = "Administrator, Admin, Manager, Service")]
    public class ImageController : Controller
    {
        private readonly ImageServiceAbstract _service;

        public ImageController(ImageServiceAbstract service)
        {
            _service = service;
        }

        [HttpPost]
        public ActionResult<List<int>> UploadImages(int itemId, [FromForm] ICollection<IFormFile> files)
        {
            var addImages = new AddImagesPOCO { Files = files, ItemId = itemId };
            try
            {
                _service.AddImages(addImages);
            }
            catch(BusinessException ex)
            {
                return Conflict(ex.Message);
            }
            return Ok();
        }

        [HttpDelete]
        public IActionResult DeleteImage(int id)
        {
            _service.Delete(id);
            return Ok();
        }
    }
}
