using Microsoft.AspNetCore.Mvc;
using centrny.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace centrny1.Controllers
{
    public class ItemController : Controller
    {
        public class InsertItemsRequest
        {
            public int RootCode { get; set; }
            public int InsertUserCode { get; set; }
            public int RecordCount { get; set; }
            public int ItemTypeCode { get; set; }
        }

        private readonly CenterContext _context;

        public ItemController(CenterContext context)
        {
            _context = context;
        }

        // --- SESSION HELPERS ---
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);
        private (int? rootCode, int? userCode, int? groupCode, string username) GetSessionContext()
        {
            return (
                GetSessionInt("RootCode"),
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                GetSessionString("Username")
            );
        }

        // --- Authority Check via Session ---
        private bool UserHasItemPermission()
        {
            // You may want to cache this permission in session at login for best performance
            var groupCode = GetSessionInt("GroupCode");
            if (groupCode == null) return false;

            var page = _context.Pages.FirstOrDefault(p => p.PagePath == "Item/Index");
            if (page == null) return false;

            return _context.GroupPages.Any(gp => gp.GroupCode == groupCode.Value && gp.PageCode == page.PageCode);
        }

        public IActionResult Index()
        {
            if (!UserHasItemPermission())
            {
                return View("~/Views/Login/AccessDenied.cshtml");
            }

            return View();
        }

        // GET: /Item/GetAllItems?page=1&pageSize=10&rootCode=123
        [HttpGet]
        public IActionResult GetAllItems(int page = 1, int pageSize = 10, int? rootCode = null)
        {
            if (!UserHasItemPermission())
                return Json(new { success = false, message = "Access denied." });

            var sessionRootCode = GetSessionInt("RootCode");
            if (sessionRootCode == null)
                return Unauthorized();

            // If rootCode is not provided, use session value.
            if (!rootCode.HasValue || rootCode.Value <= 0)
                rootCode = sessionRootCode.Value;

            // Join Items with Roots to get the root domain
            var query = (from i in _context.Items
                         join r in _context.Roots on i.RootCode equals r.RootCode
                         where i.IsActive && i.RootCode == rootCode.Value
                         join s in _context.Students on i.StudentCode equals s.StudentCode into studentJoin
                         from s in studentJoin.DefaultIfEmpty()
                         select new
                         {
                             itemCode = i.ItemCode,
                             studentName = s != null ? s.StudentName : "",
                             itemTypeKey = i.ItemTypeKey,
                             itemKey = i.ItemKey,
                             rootCodeVal = i.RootCode,
                             rootDomain = r.RootDomain // <-- Include domain!
                         });

            var totalCount = query.Count();

            var items = query
                .OrderBy(i => i.itemCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Json(new { data = items, totalCount = totalCount });
        }

        [HttpGet]
        public IActionResult GetItemTypes()
        {
            if (!UserHasItemPermission())
                return Json(new { success = false, message = "Access denied." });

            var types = _context.ItemTypes
                .Select(t => new { code = t.ItemTypeCode, name = t.ItemTypeName })
                .ToList();
            return Json(types);
        }

        [HttpGet]
        public IActionResult GetFreeItemCount(int? rootCode = null)
        {
            if (!UserHasItemPermission())
                return Json(new { success = false, message = "Access denied." });

            var sessionRootCode = GetSessionInt("RootCode");
            if (sessionRootCode == null)
                return Unauthorized();

            if (!rootCode.HasValue || rootCode.Value <= 0)
                rootCode = sessionRootCode.Value;

            var count = _context.Items
                .Where(i => i.IsActive && (i.StudentCode == null || i.StudentCode == 0) && i.RootCode == rootCode.Value)
                .Count();

            return Json(new { freeCount = count });
        }

        [HttpGet]
        public IActionResult GetRootCodes()
        {
            if (!UserHasItemPermission())
                return Json(new { success = false, message = "Access denied." });

            var roots = _context.Roots
                .Select(r => new { code = r.RootCode, name = r.RootName })
                .ToList();
            return Json(roots);
        }

        [HttpGet]
        public IActionResult GetLoggedInUserCode()
        {
            if (!UserHasItemPermission())
                return Json(new { success = false, message = "Access denied." });

            var userCode = GetSessionInt("UserCode");
            if (userCode == null)
                return Unauthorized(new { error = "User not found." });

            return Json(new { userCode });
        }

        [HttpPost]
        public IActionResult UpdateItem([FromBody] Item model)
        {
            if (!UserHasItemPermission())
                return Json(new { success = false, message = "Access denied." });

            if (model == null || model.ItemCode <= 0)
                return BadRequest("Invalid data");

            var item = _context.Items.FirstOrDefault(i => i.ItemCode == model.ItemCode);
            if (item == null)
                return NotFound("Item not found");

            item.StudentCode = model.StudentCode;
            _context.SaveChanges();

            return Ok(new { message = "Item updated successfully" });
        }

        [HttpPost]
        [Route("Item/InsertItems")]
        public IActionResult InsertItems([FromBody] InsertItemsRequest request)
        {
            if (!UserHasItemPermission())
                return Json(new { success = false, message = "Access denied." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var sessionUserCode = GetSessionInt("UserCode");
            if (sessionUserCode == null)
                return Unauthorized();

            // Use the root code sent from the frontend, not the session!
            request.InsertUserCode = sessionUserCode.Value;

            try
            {
                var sql = "EXEC InsertIntoItem @RootCode, @InsertUser, @RecordCount, @ItemTypeCode";

                _context.Database.ExecuteSqlRaw(
                    sql,
                    new SqlParameter("@RootCode", request.RootCode),
                    new SqlParameter("@InsertUser", request.InsertUserCode),
                    new SqlParameter("@RecordCount", request.RecordCount),
                    new SqlParameter("@ItemTypeCode", request.ItemTypeCode)
                );

                var lastInsertedItems = (from i in _context.Items
                                         join r in _context.Roots on i.RootCode equals r.RootCode
                                         where i.RootCode == request.RootCode
                                               && i.ItemTypeKey == request.ItemTypeCode
                                               && i.IsActive
                                         orderby i.ItemCode descending
                                         select new
                                         {
                                             itemCode = i.ItemCode,
                                             itemKey = i.ItemKey,
                                             rootDomain = r.RootDomain
                                         })
                              .Take(request.RecordCount)
                              .ToList();
                lastInsertedItems.Reverse();

                return Ok(new
                {
                    message = "Items inserted successfully.",
                    lastInsertedItems = lastInsertedItems
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        [Route("Item/SoftDeleteItem")]
        public IActionResult SoftDeleteItem([FromBody] int itemCode)
        {
            if (!UserHasItemPermission())
                return Json(new { success = false, message = "Access denied." });

            if (itemCode <= 0)
                return BadRequest("Invalid item code.");

            var item = _context.Items.FirstOrDefault(i => i.ItemCode == itemCode);
            if (item == null)
                return NotFound();

            item.IsActive = false;
            _context.SaveChanges();

            return Ok(new { message = "Item deleted successfully." });
        }
    }

}