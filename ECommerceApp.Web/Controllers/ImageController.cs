using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
    public class ImageController : Controller
    {
        private readonly IImageService _service;

        public ImageController(IImageService service)
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
