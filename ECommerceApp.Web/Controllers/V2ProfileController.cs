using System.Threading.Tasks;
using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.AccountProfile.ViewModels;
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
        public async Task<IActionResult> Add(CreateProfileFormVm form)
        {
            var profileId = await _profileService.CreateAsync(
                new CreateUserProfileDto(form.UserId, form.FirstName, form.LastName, form.IsCompany,
                    string.IsNullOrWhiteSpace(form.Nip) ? null : form.Nip,
                    string.IsNullOrWhiteSpace(form.CompanyName) ? null : form.CompanyName,
                    form.Email, form.PhoneNumber));
            await _profileService.AddAddressAsync(profileId, form.UserId,
                new AddAddressDto(profileId, form.Street, form.BuildingNumber, form.FlatNumber, form.ZipCode, form.City, form.Country));
            TempData["Success"] = "User profile created.";
            return RedirectToAction(nameof(Details), new { id = profileId });
        }

        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _profileService.GetDetailsAsync(id);
            return vm is null ? NotFound() : View(vm);
        }

        [HttpPost("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id, EditProfileFormVm form)
        {
            await _profileService.UpdatePersonalInfoAsync(
                new UpdateUserProfileDto(id, form.FirstName, form.LastName, form.IsCompany,
                    string.IsNullOrWhiteSpace(form.Nip) ? null : form.Nip,
                    string.IsNullOrWhiteSpace(form.CompanyName) ? null : form.CompanyName));
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

        [HttpPost("{id:int}/addresses/add")]
        public async Task<IActionResult> AddAddress(int id, AddressFormVm form)
        {
            var vm = await _profileService.GetDetailsAsync(id);
            if (vm is null) return NotFound();
            await _profileService.AddAddressAsync(id, vm.UserId,
                new AddAddressDto(id, form.Street, form.BuildingNumber, form.FlatNumber, form.ZipCode, form.City, form.Country));
            TempData["Success"] = "Address added.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost("{id:int}/addresses/{addressId:int}/remove")]
        public async Task<IActionResult> RemoveAddress(int id, int addressId)
        {
            var vm = await _profileService.GetDetailsAsync(id);
            if (vm is null) return NotFound();
            await _profileService.RemoveAddressAsync(id, addressId, vm.UserId);
            TempData["Success"] = "Address removed.";
            return RedirectToAction(nameof(Edit), new { id });
        }
    }
}
