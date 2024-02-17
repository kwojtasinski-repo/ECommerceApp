using System.Threading.Tasks;
using ECommerceApp.Application.Exceptions;
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
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("userNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new NewUserVm());
            }
            return View(userVm);
        }

        [HttpPost]
        public async Task<IActionResult> AddRolesToUser(NewUserVm user)
        {
            try
            {
                if ((await _userService.ChangeRoleAsync(user.Id, user.UserRole)) is null)
                {
                    var errorModel = BuildErrorModel(ErrorCode.Create("userNotFound", ErrorParameter.Create("id", user.Id)));
                    return RedirectToAction("Index", errorModel.AsOjectRoute());
                }
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
                return (await _userService.DeleteUserAsync(id)) is not null
                    ? Json("deleted")
                    : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userService.GetUserById(id);
            if (user is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("userNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new NewUserVm());
            }
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(NewUserVm model)
        {
            try
            {
                if ((await _userService.EditUser(model)) is null)
                {
                    var errorModel = BuildErrorModel(ErrorCode.Create("userNotFound", ErrorParameter.Create("id", model.Id)));
                    return RedirectToAction("Index", errorModel.AsOjectRoute());
                }
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
            var newUser = new NewUserToAddVm
            {
                Roles = await _userService.GetAllRoles()
            };
            return View(newUser);
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(NewUserToAddVm model)
        {
            try
            {
                await _userService.AddUser(model);
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
            var user = await _userService.GetUserById(id);
            if (user is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("userNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new NewUserVm());
            }
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeUserPassword(NewUserVm model)
        {
            try
            {
                if ((await _userService.ChangeUserPassword(model)) is null)
                {
                    var errorModel = BuildErrorModel(ErrorCode.Create("userNotFound", ErrorParameter.Create("id", model.Id)));
                    return RedirectToAction("Index", errorModel.AsOjectRoute());
                }
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }
    }
}
