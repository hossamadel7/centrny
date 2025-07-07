using Microsoft.AspNetCore.Mvc;
using centrny.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
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

        public IActionResult Index()
        {
            return View();
        }

        // GET: /Item/GetAllItems?page=1&pageSize=10&rootCode=123
        [HttpGet]
        public IActionResult GetAllItems(int page = 1, int pageSize = 10, int? rootCode = null)
        {
            var query = (from i in _context.Items
                         where i.IsActive
                         join s in _context.Students on i.StudentCode equals s.StudentCode into studentJoin
                         from s in studentJoin.DefaultIfEmpty()
                         select new
                         {
                             itemCode = i.ItemCode,
                             studentName = s != null ? s.StudentName : "",
                             itemTypeKey = i.ItemTypeKey,
                             itemKey = i.ItemKey,
                             rootCodeVal = i.RootCode
                         });

            if (rootCode.HasValue && rootCode.Value > 0)
            {
                query = query.Where(i => i.rootCodeVal == rootCode.Value);
            }

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
            var types = _context.ItemTypes
                .Select(t => new { code = t.ItemTypeCode, name = t.ItemTypeName })
                .ToList();
            return Json(types);
        }

        [HttpGet]
        public IActionResult GetFreeItemCount(int? rootCode = null)
        {
            var query = _context.Items
                .Where(i => i.IsActive && (i.StudentCode == null || i.StudentCode == 0));

            if (rootCode.HasValue && rootCode.Value > 0)
            {
                query = query.Where(i => i.RootCode == rootCode.Value);
            }

            var count = query.Count();
            return Json(new { freeCount = count });
        }

        [HttpGet]
        public IActionResult GetRootCodes()
        {
            var roots = _context.Roots
                .Select(r => new { code = r.RootCode, name = r.RootName })
                .ToList();
            return Json(roots);
        }

        [HttpGet]
        public IActionResult GetLoggedInUserCode()
        {
            int? userCode = null;

            // Use claim "UserCode" if available
            var claim = User.Claims.FirstOrDefault(c => c.Type == "UserCode");
            if (claim != null && int.TryParse(claim.Value, out var codeFromClaim))
                userCode = codeFromClaim;
            // Fallback to username mapping
            else if (!string.IsNullOrEmpty(User.Identity.Name))
            {
                var user = _context.Users.FirstOrDefault(u => u.Username == User.Identity.Name);
                if (user != null)
                    userCode = user.UserCode;
            }

            if (userCode == null)
                return Unauthorized(new { error = "User not found." });

            return Json(new { userCode });
        }

        [HttpPost]
        public IActionResult UpdateItem([FromBody] Item model)
        {
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
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

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

                // Immediately fetch the last N items for this root/type (assuming sequential codes).
                var lastInsertedItems = _context.Items
                    .Where(i => i.RootCode == request.RootCode
                             && i.ItemTypeKey == request.ItemTypeCode
                             && i.IsActive)
                    .OrderByDescending(i => i.ItemCode)
                    .Take(request.RecordCount)
                    .Select(i => new { itemCode = i.ItemCode, itemKey = i.ItemKey })
                    .ToList();

                // Return in ascending order (oldest to newest)
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