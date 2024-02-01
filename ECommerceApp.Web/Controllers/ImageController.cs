using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.POCO;
using ECommerceApp.Application.Services.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = $"{MaintenanceRole}")]
    public class ImageController : BaseController
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
            catch(BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
            return Ok();
        }

        [HttpDelete]
        public IActionResult DeleteImage(int id)
        {
            try
            {
                _service.Delete(id);
                return Ok();
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }
    }
}
