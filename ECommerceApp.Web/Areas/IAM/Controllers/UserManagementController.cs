using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Identity.IAM.ViewModels;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.IAM.Controllers
{
    [Area("IAM")]
    [Authorize(Roles = $"{ManagingRole}")]
    public class UserManagementController : BaseController
    {
        private readonly IUserManagementService _userManagementService;

        public UserManagementController(IUserManagementService userManagementService)
        {
            _userManagementService = userManagementService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _userManagementService.GetUsersAsync(20, 1, "");
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var model = await _userManagementService.GetUsersAsync(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AddRolesToUser(string id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                return View(user);
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddRolesToUser(UserDetailsVm user)
        {
            try
            {
                await _userManagementService.ChangeUserRoleAsync(user.Id, user.UserRole);
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                await _userManagementService.DeleteUserAsync(id);
                return Json("deleted");
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                return View(user);
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(UserDetailsVm model)
        {
            try
            {
                await _userManagementService.UpdateUserAsync(model);
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        [HttpGet]
        public async Task<IActionResult> AddUser()
        {
            var vm = new CreateUserVm
            {
                AvailableRoles = await _userManagementService.GetRolesAsync()
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(CreateUserVm model)
        {
            try
            {
                await _userManagementService.CreateUserAsync(model);
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ChangeUserPassword(string id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                return View(user);
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        [HttpPost]
        public async Task<IActionResult> ChangeUserPassword(UserDetailsVm model)
        {
            try
            {
                await _userManagementService.ChangePasswordAsync(model.Id, model.NewPassword);
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }
    }
}
