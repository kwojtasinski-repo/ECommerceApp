using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/v2")]
    public class CatalogController : BaseController
    {
        private readonly IProductService _products;
        private readonly ICategoryService _categories;
        private readonly IProductTagService _tags;

        public CatalogController(
            IProductService products,
            ICategoryService categories,
            IProductTagService tags)
        {
            _products = products;
            _categories = categories;
            _tags = tags;
        }

        // ── Products ─────────────────────────────────────────────────────────

        [HttpGet("products")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageNo = 1,
            [FromQuery] string searchString = "",
            CancellationToken ct = default)
        {
            var vm = await _products.GetAllProducts(pageSize, pageNo, searchString ?? "");
            return Ok(vm);
        }

        [HttpGet("products/published")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublishedProducts(
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageNo = 1,
            [FromQuery] string searchString = "",
            CancellationToken ct = default)
        {
            var vm = await _products.GetPublishedProducts(pageSize, pageNo, searchString ?? "");
            return Ok(vm);
        }

        [HttpGet("products/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProduct(int id, CancellationToken ct = default)
        {
            var vm = await _products.GetProductDetails(id, ct);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpPost("products")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
        {
            var id = await _products.AddProduct(dto);
            return StatusCode(StatusCodes.Status201Created, new { id });
        }

        [HttpPut("products")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> UpdateProduct([FromBody] UpdateProductDto dto)
        {
            var updated = await _products.UpdateProduct(dto);
            return updated ? Ok() : NotFound();
        }

        [HttpDelete("products/{id:int}")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var deleted = await _products.DeleteProduct(id);
            return deleted ? NoContent() : NotFound();
        }

        [HttpPut("products/{id:int}/publish")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> PublishProduct(int id)
        {
            await _products.PublishProduct(id);
            return Ok();
        }

        [HttpPut("products/{id:int}/unpublish")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> UnpublishProduct(int id)
        {
            await _products.UnpublishProduct(id);
            return Ok();
        }

        // ── Categories ───────────────────────────────────────────────────────

        [HttpGet("categories")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories()
        {
            var list = await _categories.GetAllCategories();
            return Ok(list);
        }

        [HttpGet("categories/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategory(int id)
        {
            var vm = await _categories.GetCategory(id);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpPost("categories")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            var id = await _categories.AddCategory(dto);
            return StatusCode(StatusCodes.Status201Created, new { id });
        }

        [HttpPut("categories")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> UpdateCategory([FromBody] UpdateCategoryDto dto)
        {
            var updated = await _categories.UpdateCategory(dto);
            return updated ? Ok() : NotFound();
        }

        [HttpDelete("categories/{id:int}")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var deleted = await _categories.DeleteCategory(id);
            return deleted ? NoContent() : NotFound();
        }

        // ── Tags ─────────────────────────────────────────────────────────────

        [HttpGet("tags")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTags()
        {
            var list = await _tags.GetAllTags();
            return Ok(list);
        }

        [HttpGet("tags/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTag(int id)
        {
            var vm = await _tags.GetTag(id);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpGet("tags/search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchTags(
            [FromQuery] string q,
            [FromQuery] int maxResults = 10)
        {
            var list = await _tags.SearchTags(q ?? "", maxResults);
            return Ok(list);
        }

        [HttpPost("tags")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> CreateTag([FromBody] CreateTagDto dto)
        {
            var id = await _tags.AddTag(dto);
            return StatusCode(StatusCodes.Status201Created, new { id });
        }

        [HttpPut("tags")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> UpdateTag([FromBody] UpdateTagDto dto)
        {
            var updated = await _tags.UpdateTag(dto);
            return updated ? Ok() : NotFound();
        }

        [HttpDelete("tags/{id:int}")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> DeleteTag(int id)
        {
            var deleted = await _tags.DeleteTag(id);
            return deleted ? NoContent() : NotFound();
        }
    }
}
