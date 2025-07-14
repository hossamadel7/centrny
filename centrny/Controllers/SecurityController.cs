using centrny.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace centrny.Controllers
{
    [Authorize]
    public class SecurityController : Controller
    {
        private readonly CenterContext _context;

        public SecurityController(CenterContext context)
        {
            _context = context;
        }

        // --- Authority Check ---
        private bool UserHasSecurityPermission()
        {
            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
                return false;

            var userGroupCodes = _context.Users
                .Where(ug => ug.UserCode == user.UserCode)
                .Select(ug => ug.GroupCode)
                .ToList();

            // Use your page path as stored in Pages (adjust as needed)
            var page = _context.Pages.FirstOrDefault(p => p.PagePath == "Security/Index");
            if (page == null)
                return false;

            return _context.GroupPages.Any(gp => userGroupCodes.Contains(gp.GroupCode) && gp.PageCode == page.PageCode);
        }

        public IActionResult Index()
        {
            if (!UserHasSecurityPermission())
            {
                return View("~/Views/Login/AccessDenied.cshtml");
            }
            return View();
        }

        [HttpGet]
        public JsonResult GetRoots(bool? isCenter = null)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var query = _context.Roots.AsQueryable();
            if (isCenter.HasValue)
            {
                query = query.Where(r => r.IsCenter == isCenter.Value);
            }
            var roots = query
                .Select(r => new { r.RootCode, r.RootName })
                .ToList();
            return Json(roots);
        }

        [HttpGet]
        public JsonResult GetGroupsByRoot(int rootCode)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var groups = _context.Groups
                .Where(g => g.RootCode == rootCode)
                .Select(g => new { g.GroupCode, g.GroupName, g.GroupDesc, g.RootCode })
                .ToList();

            if (groups.Count == 0)
                return Json(new { success = true, groups = new List<object>() });

            return Json(new { success = true, groups = groups });
        }

        [HttpPost]
        public JsonResult CreateGroup(string groupName, string groupDesc, int rootCode, int insertUser)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            try
            {
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    return Json(new { success = false, message = "Group name is required" });
                }
                var group = new Group
                {
                    GroupName = groupName,
                    GroupDesc = groupDesc,
                    RootCode = rootCode,
                    InsertUser = insertUser,
                    InsertTime = DateTime.Now
                };
                _context.Groups.Add(group);
                _context.SaveChanges();
                return Json(new { success = true, group = new { group.GroupCode, group.GroupName, group.GroupDesc, group.RootCode } });
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    message += " | Inner: " + inner.Message;
                    inner = inner.InnerException;
                }
                return Json(new { success = false, message });
            }
        }

        [HttpPost]
        public JsonResult EditGroup(int groupCode, string groupName, string groupDesc)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var group = _context.Groups.FirstOrDefault(g => g.GroupCode == groupCode);
            if (group == null)
                return Json(new { success = false, message = "Group not found" });

            if (!string.IsNullOrWhiteSpace(groupName))
                group.GroupName = groupName;
            group.GroupDesc = groupDesc;

            _context.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult DeleteGroup(int groupCode)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var group = _context.Groups.FirstOrDefault(g => g.GroupCode == groupCode);
            if (group == null)
                return Json(new { success = false, message = "Group not found" });

            // Check for referencing users
            var hasUsers = _context.Users.Any(u => u.GroupCode == groupCode && u.IsActive);
            if (hasUsers)
                return Json(new { success = false, message = "Cannot delete group with active users. Remove users first." });

            _context.Groups.Remove(group);
            _context.SaveChanges();
            return Json(new { success = true });
        }

        [HttpGet]
        public JsonResult GetUsersByGroup(int groupCode)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var users = _context.Users
                .Where(u => u.GroupCode == groupCode && u.IsActive)
                .Select(u => new { u.UserCode, u.Name, u.Username, u.IsActive })
                .ToList();

            return Json(users);
        }

        [HttpPost]
        public JsonResult EditUser(int userCode, string name, bool isActive)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var user = _context.Users.FirstOrDefault(u => u.UserCode == userCode);
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            if (!string.IsNullOrEmpty(name))
                user.Name = name;
            user.IsActive = isActive;

            _context.SaveChanges();

            return Json(new { success = true });
        }
        [HttpPost]
        public JsonResult ResetUserPassword(int userCode)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var user = _context.Users.FirstOrDefault(u => u.UserCode == userCode);
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.Unicode.GetBytes("123456789");
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                    sb.Append(hashBytes[i].ToString("X2"));
                user.Password = sb.ToString();
            }
            _context.SaveChanges();

            return Json(new { success = true, message = "Password reset to 123456789" });
        }

        [HttpPost]
        public JsonResult DeleteUser(int userCode)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var user = _context.Users.FirstOrDefault(u => u.UserCode == userCode);
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            user.IsActive = false;
            _context.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult CreateUser(string name, string username, string password, int groupCode, bool isActive, int insertUserCode)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            try
            {
                string usernameToCheck = (username ?? "").Trim().ToLower();
                bool usernameExists = _context.Users.Any(u => u.Username.ToLower() == usernameToCheck);
                if (usernameExists)
                {
                    return Json(new { success = false, message = "Username already exists" });
                }

                int lastUserCode = _context.Users.Any() ? _context.Users.Max(u => u.UserCode) : 0;
                int newUserCode = lastUserCode + 1;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username))
                    return Json(new { success = false, message = "All fields are required" });

                string actualUsername = username;

                string hashedPassword;
                using (MD5 md5 = MD5.Create())
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes("123456789");
                    byte[] hashBytes = md5.ComputeHash(inputBytes);
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < hashBytes.Length; i++)
                        sb.Append(hashBytes[i].ToString("x2"));
                    hashedPassword = sb.ToString();
                }

                DateTime insertTime = DateTime.Now;

                var user = new User
                {
                    UserCode = newUserCode,
                    Name = name,
                    Username = actualUsername,
                    Password = hashedPassword,
                    GroupCode = groupCode,
                    IsActive = isActive,
                    InsertUser = insertUserCode,
                    InsertTime = insertTime
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                return Json(new { success = true, userCode = newUserCode, username = actualUsername });
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    message += " | Inner: " + inner.Message;
                    inner = inner.InnerException;
                }
                return Json(new { success = false, message });
            }
        }

        [HttpGet]
        public JsonResult IsUsernameTaken(string username)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            username = (username ?? "").Trim().ToLower();
            bool taken = _context.Users.Any(u => u.Username.ToLower() == username);
            return Json(new { taken });
        }
    }
}