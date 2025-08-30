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

        // --- SESSION HELPERS ---
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);
        private (int? userCode, int? groupCode, int? rootCode, string username) GetSessionContext()
        {
            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                GetSessionInt("RootCode"),
                GetSessionString("Username")
            );
        }

        // Use Unicode encoding to match SQL Server NVARCHAR hashing
        public static string MD5hasher(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.Unicode.GetBytes(input ?? "");
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                    sb.Append(b.ToString("X2")); // Uppercase hex to match SQL output
                return sb.ToString();
            }
        }

        // --- Authority Check ---
        private bool UserHasSecurityPermission()
        {
            var groupCode = GetSessionInt("GroupCode");
            if (groupCode == null) return false;

            var page = _context.Pages.FirstOrDefault(p => p.PagePath == "Security/Index");
            if (page == null) return false;

            return _context.GroupPages.Any(gp => gp.GroupCode == groupCode.Value && gp.PageCode == page.PageCode);
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
        public JsonResult GetBranchesByRoot(int rootCode)
        {
            if (!UserHasSecurityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var branches = _context.Branches
                .Where(b => b.RootCode == rootCode)
                .Select(b => new { b.BranchCode, b.BranchName })
                .ToList();

            return Json(branches);
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
                .Select(g => new { g.GroupCode, g.GroupName, g.GroupDesc, g.RootCode, g.BranchCode })
                .ToList();

            if (groups.Count == 0)
                return Json(new { success = true, groups = new List<object>() });

            return Json(new { success = true, groups = groups });
        }

        [HttpPost]
        public JsonResult CreateGroup(string groupName, string groupDesc, int rootCode, int insertUser, int branchCode)
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
                    InsertTime = DateTime.Now,
                    BranchCode = branchCode
                };
                _context.Groups.Add(group);
                _context.SaveChanges();
                return Json(new { success = true, group = new { group.GroupCode, group.GroupName, group.GroupDesc, group.RootCode, group.BranchCode } });
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
        public JsonResult EditGroup(int groupCode, string groupName, string groupDesc, int branchCode)
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
            group.BranchCode = branchCode;

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

            var users = _context.Users.Where(u => u.GroupCode == groupCode).ToList();
            if (users.Any())
            {
                _context.Users.RemoveRange(users);
            }
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
        public JsonResult EditUser(int userCode, string name, bool isActive, string password = null)
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

            // Save password as Unicode MD5 hash if provided
            if (!string.IsNullOrWhiteSpace(password))
            {
                user.Password = MD5hasher(password);
            }

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

            // Set password as Unicode MD5 hash of default
            user.Password = MD5hasher("123456789");
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

                // Save password as Unicode MD5 hash
                string plainPassword = string.IsNullOrWhiteSpace(password) ? "123456789" : password;
                string hashedPassword = MD5hasher(plainPassword);
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