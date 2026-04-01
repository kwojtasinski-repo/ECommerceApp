using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
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

        public ImageController(IImageService service)
        {
            _service = service;
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
