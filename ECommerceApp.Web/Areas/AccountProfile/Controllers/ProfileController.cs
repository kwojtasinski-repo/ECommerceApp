using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.AccountProfile.ViewModels;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.AccountProfile.Controllers
{
    [Area("AccountProfile")]
    [Authorize]
    public class ProfileController : BaseController
    {
        private readonly IUserProfileService _profileService;

        public ProfileController(IUserProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            var model = await _profileService.GetAllByUserIdAsync(userId, 10, 1, string.Empty);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string? searchString)
        {
            pageNo ??= 1;
            searchString ??= string.Empty;
            var userId = GetUserId();
            var model = await _profileService.GetAllByUserIdAsync(userId, pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var vm = await _profileService.GetDetailsAsync(id);
            if (vm is null)
                return NotFound();

            if (!MaintenanceRoles.Any(r => User.IsInRole(r)) && vm.UserId != GetUserId())
                return Forbid();

            return View(vm);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateProfileFormVm { UserId = GetUserId() });
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateProfileFormVm model)
        {
            var userId = GetUserId();
            var profileId = await _profileService.CreateAsync(new CreateUserProfileDto(
                userId,
                model.FirstName,
                model.LastName,
                model.IsCompany,
                model.Nip,
                model.CompanyName,
                model.Email,
                model.PhoneNumber));

            if (!string.IsNullOrWhiteSpace(model.Street))
            {
                await _profileService.AddAddressAsync(profileId, userId, new AddAddressDto(
                    profileId,
                    model.Street,
                    model.BuildingNumber,
                    model.FlatNumber,
                    model.ZipCode,
                    model.City,
                    model.Country));
            }

            return RedirectToAction(nameof(Details), new { id = profileId });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _profileService.GetAsync(id, GetUserId());
            if (vm is null)
                return NotFound();

            ViewBag.ProfileId = id;
            return View(new EditProfileFormVm
            {
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                IsCompany = vm.IsCompany,
                CompanyName = vm.CompanyName,
                Nip = vm.NIP
            });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, EditProfileFormVm model)
        {
            await _profileService.UpdatePersonalInfoAsync(new UpdateUserProfileDto(
                id,
                model.FirstName,
                model.LastName,
                model.IsCompany,
                model.Nip,
                model.CompanyName));

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> EditContactInfo(int id)
        {
            var vm = await _profileService.GetAsync(id, GetUserId());
            if (vm is null)
                return NotFound();

            ViewBag.ProfileId = id;
            ViewBag.Email = vm.Email;
            ViewBag.PhoneNumber = vm.PhoneNumber;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EditContactInfo(int id, string email, string phoneNumber)
        {
            await _profileService.UpdateContactInfoAsync(new UpdateContactInfoDto(id, email, phoneNumber));
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> AddAddress(int userProfileId)
        {
            if (!await _profileService.ExistsAsync(userProfileId, GetUserId()))
                return NotFound();

            ViewBag.UserProfileId = userProfileId;
            return View(new AddressFormVm());
        }

        [HttpPost]
        public async Task<IActionResult> AddAddress(int userProfileId, AddressFormVm model)
        {
            await _profileService.AddAddressAsync(userProfileId, GetUserId(), new AddAddressDto(
                userProfileId,
                model.Street,
                model.BuildingNumber,
                model.FlatNumber,
                model.ZipCode,
                model.City,
                model.Country));

            return RedirectToAction(nameof(Details), new { id = userProfileId });
        }

        [HttpGet]
        public async Task<IActionResult> EditAddress(int userProfileId, int addressId)
        {
            var vm = await _profileService.GetDetailsAsync(userProfileId);
            if (vm is null)
                return NotFound();

            if (!MaintenanceRoles.Any(r => User.IsInRole(r)) && vm.UserId != GetUserId())
                return Forbid();

            var address = vm.Addresses.Find(a => a.Id == addressId);
            if (address is null)
                return NotFound();

            ViewBag.UserProfileId = userProfileId;
            ViewBag.AddressId = addressId;
            return View(new AddressFormVm
            {
                Street = address.Street,
                BuildingNumber = address.BuildingNumber,
                FlatNumber = address.FlatNumber,
                ZipCode = address.ZipCode,
                City = address.City,
                Country = address.Country
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditAddress(int userProfileId, int addressId, AddressFormVm model)
        {
            await _profileService.UpdateAddressAsync(userProfileId, GetUserId(), new UpdateAddressDto(
                addressId,
                model.Street,
                model.BuildingNumber,
                model.FlatNumber,
                model.ZipCode,
                model.City,
                model.Country));

            return RedirectToAction(nameof(Details), new { id = userProfileId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAddress(int userProfileId, int addressId)
        {
            await _profileService.RemoveAddressAsync(userProfileId, addressId, GetUserId());
            return RedirectToAction(nameof(Details), new { id = userProfileId });
        }

        [HttpGet]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> All()
        {
            var model = await _profileService.GetAllAsync(20, 1, string.Empty);
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> All(int pageSize, int? pageNo, string? searchString)
        {
            pageNo ??= 1;
            searchString ??= string.Empty;
            var model = await _profileService.GetAllAsync(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> Delete(int id)
        {
            await _profileService.DeleteAsync(id);
            return RedirectToAction(nameof(All));
        }
    }
}
