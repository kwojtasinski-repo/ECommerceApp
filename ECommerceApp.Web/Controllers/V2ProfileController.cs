using System.Threading.Tasks;
using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Route("v2/profiles")]
    public class V2ProfileController : Controller
    {
        private readonly IUserProfileService _profileService;

        public V2ProfileController(IUserProfileService profileService) =>
            _profileService = profileService;

        [HttpGet("")]
        public async Task<IActionResult> Index() =>
            View(await _profileService.GetAllAsync(50, 1, string.Empty));

        [HttpGet("details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var vm = await _profileService.GetDetailsAsync(id);
            return vm is null ? NotFound() : View(vm);
        }

        [HttpGet("add")]
        public IActionResult Add() => View();

        [HttpPost("add")]
        public async Task<IActionResult> Add(
            string userId, string firstName, string lastName, bool isCompany,
            string nip, string companyName, string email, string phoneNumber)
        {
            await _profileService.CreateAsync(
                new CreateUserProfileDto(userId, firstName, lastName, isCompany,
                    string.IsNullOrWhiteSpace(nip) ? null : nip,
                    string.IsNullOrWhiteSpace(companyName) ? null : companyName,
                    email, phoneNumber));
            TempData["Success"] = "User profile created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _profileService.GetDetailsAsync(id);
            return vm is null ? NotFound() : View(vm);
        }

        [HttpPost("edit/{id:int}")]
        public async Task<IActionResult> Edit(
            int id, string firstName, string lastName, bool isCompany,
            string nip, string companyName)
        {
            await _profileService.UpdatePersonalInfoAsync(
                new UpdateUserProfileDto(id, firstName, lastName, isCompany,
                    string.IsNullOrWhiteSpace(nip) ? null : nip,
                    string.IsNullOrWhiteSpace(companyName) ? null : companyName));
            TempData["Success"] = "User profile updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _profileService.DeleteAsync(id);
            TempData["Success"] = "User profile deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
