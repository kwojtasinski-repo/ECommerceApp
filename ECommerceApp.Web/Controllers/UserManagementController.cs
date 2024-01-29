using System.Threading.Tasks;
using ECommerceApp.Application.Services.Users;
using ECommerceApp.Application.ViewModels.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = $"{ManagingRole}")]
    public class UserManagementController : BaseController
    {
        private readonly IUserService _userService;

        public UserManagementController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _userService.GetAllUsers(20, 1, "");
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
            var model = await _userService.GetAllUsers(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AddRolesToUser(string id)
        {
            var userVm = await _userService.GetUserById(id);
            if (userVm is null)
            {
                return NotFound();
            }
            return View(userVm);
        }

        [HttpPost]
        public async Task<IActionResult> AddRolesToUser(NewUserVm user)
        {
            await _userService.ChangeRoleAsync(user.Id, user.UserRoles);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> DeleteUser(string id)
        {
            await _userService.DeleteUserAsync(id);
            return Json("deleted");
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userService.GetUserById(id);
            if (user is null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(NewUserVm model)
        {
            await _userService.EditUser(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> AddUser()
        {
            var newUser = new NewUserToAddVm
            {
                Roles = await _userService.GetAllRoles()
            };
            return View(newUser);
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(NewUserToAddVm model)
        {
            await _userService.AddUser(model);
            if (model.Id != null)
            {
                model.UserName = model.Email;
                await _userService.ChangeRoleAsync(model.Id, model.UserRoles);
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> ChangeUserPassword(string id)
        {
            var user = await _userService.GetUserById(id);
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeUserPassword(NewUserVm model)
        {
            await _userService.ChangeUserPassword(model);
            return RedirectToAction("Index");
        }
    }
}
