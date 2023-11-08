﻿using System;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.User;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = $"{UserPermissions.Roles.Administrator}")]
    public class UserManagementController : Controller
    {
        private readonly IUserService _userService;

        public UserManagementController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = _userService.GetAllUsers(20, 1, "");
            return View(model);
        }

        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var model = _userService.GetAllUsers(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [HttpGet]
        public IActionResult AddRolesToUser(string id)
        {
            var userVm = _userService.GetUserById(id);
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
        public IActionResult EditUser(string id)
        {
            var user = _userService.GetUserById(id);
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
        public IActionResult AddUser()
        {
            var newUser = new NewUserToAddVm
            {
                Roles = _userService.GetAllRoles().ToList()
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
        public IActionResult ChangeUserPassword(string id)
        {
            var user = _userService.GetUserById(id);
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
