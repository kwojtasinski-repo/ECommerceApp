using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.Application.Supporting.Currencies.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.Supporting
{
    [Authorize]
    [Route("api/currencies")]
    public class CurrenciesController : BaseController
    {
        private readonly ICurrencyService _currencies;
        private readonly ICurrencyRateService _rates;

        public CurrenciesController(ICurrencyService currencies, ICurrencyRateService rates)
        {
            _currencies = currencies;
            _rates = rates;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageNo = 1,
            [FromQuery] string searchString = "")
        {
            var vm = await _currencies.GetAllAsync(pageSize, pageNo, searchString ?? "");
            return Ok(vm);
        }

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var vm = await _currencies.GetByIdAsync(id);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpPost]
        [Authorize(Roles = ManagingRole)]
        public async Task<IActionResult> Create([FromBody] CreateCurrencyDto dto)
        {
            var id = await _currencies.AddAsync(dto);
            return StatusCode(StatusCodes.Status201Created, new { id });
        }

        [HttpPut]
        [Authorize(Roles = ManagingRole)]
        public async Task<IActionResult> Update([FromBody] UpdateCurrencyDto dto)
        {
            var updated = await _currencies.UpdateAsync(dto);
            return updated ? Ok() : NotFound();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = ManagingRole)]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _currencies.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }

        [HttpGet("{id:int}/rate/latest")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLatestRate(int id)
        {
            var vm = await _rates.GetLatestRateAsync(id);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpGet("{id:int}/rate")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRateForDay(int id, [FromQuery] DateTime date)
        {
            var vm = await _rates.GetRateForDayAsync(id, date);
            return vm is null ? NotFound() : Ok(vm);
        }
    }
}
