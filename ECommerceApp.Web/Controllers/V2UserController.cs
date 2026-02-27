using System.Threading.Tasks;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Identity.IAM.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Route("v2/users")]
    public class V2UserController : Controller
    {
        private readonly IUserManagementService _userService;

        public V2UserController(IUserManagementService userService) =>
            _userService = userService;

        [HttpGet("")]
        public async Task<IActionResult> Index() =>
            View(await _userService.GetUsersAsync(50, 1, string.Empty));

        [HttpGet("details/{id}")]
        public async Task<IActionResult> Details(string id)
        {
            var vm = await _userService.GetUserByIdAsync(id);
            if (vm is null) return NotFound();
            vm.UserRole = await _userService.GetUserRoleAsync(id) ?? string.Empty;
            vm.AvailableRoles = await _userService.GetRolesAsync();
            return View(vm);
        }

        [HttpGet("add")]
        public async Task<IActionResult> Add()
        {
            var vm = new CreateUserVm { AvailableRoles = await _userService.GetRolesAsync() };
            return View(vm);
        }

        [HttpPost("add")]
        public async Task<IActionResult> Add(CreateUserVm vm)
        {
            vm.AvailableRoles = await _userService.GetRolesAsync();
            if (!ModelState.IsValid) return View(vm);
            await _userService.CreateUserAsync(vm);
            TempData["Success"] = "User created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(string id)
        {
            var vm = await _userService.GetUserByIdAsync(id);
            if (vm is null) return NotFound();
            vm.UserRole = await _userService.GetUserRoleAsync(id) ?? string.Empty;
            vm.AvailableRoles = await _userService.GetRolesAsync();
            return View(vm);
        }

        [HttpPost("edit/{id}")]
        public async Task<IActionResult> Edit(string id, UserDetailsVm vm)
        {
            vm.Id = id;
            await _userService.UpdateUserAsync(vm);
            if (!string.IsNullOrWhiteSpace(vm.UserRole))
                await _userService.ChangeUserRoleAsync(id, vm.UserRole);
            TempData["Success"] = "User updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _userService.DeleteUserAsync(id);
            TempData["Success"] = "User deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
