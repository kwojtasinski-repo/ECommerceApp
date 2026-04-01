using ECommerceApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [AllowAnonymous]
    [Route("catalog/images")]
    public class ImagesController : Controller
    {
        private readonly IUrlImageResolver _resolver;

        public ImagesController(IUrlImageResolver resolver)
        {
            _resolver = resolver;
        }

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
    }
}
