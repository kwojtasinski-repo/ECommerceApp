using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Controllers
{
    [Route("image")]
    public class ImageController : Controller
    {
        [HttpPost("{itemId}")]
        public ActionResult<List<int>> UploadImages(int itemId, [FromForm] ICollection<IFormFile> files)
        {
            return Ok();
        }
    }
}
