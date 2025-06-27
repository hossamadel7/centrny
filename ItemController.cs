using Microsoft.AspNetCore.Mvc;
using centrny1.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;

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

        // GET: /Item/GetAllItems?page=1&pageSize=10
        [HttpGet]
        public IActionResult GetAllItems(int page = 1, int pageSize = 10)
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
            var types = _context.ItemTypes
                .Select(t => new { code = t.ItemTypeCode, name = t.ItemTypeName })
                .ToList();
            return Json(types);
        }

        [HttpGet]
        public IActionResult GetFreeItemCount()
        {
            // Free items: IsActive, and StudentCode is null or 0
            var count = _context.Items
                .Where(i => i.IsActive && (i.StudentCode == null || i.StudentCode == 0))
                .Count();
            return Json(new { freeCount = count });
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
                    new Microsoft.Data.SqlClient.SqlParameter("@RootCode", request.RootCode),
                    new Microsoft.Data.SqlClient.SqlParameter("@InsertUser", request.InsertUserCode),
                    new Microsoft.Data.SqlClient.SqlParameter("@RecordCount", request.RecordCount),
                    new Microsoft.Data.SqlClient.SqlParameter("@ItemTypeCode", request.ItemTypeCode)
                );

                return Ok(new { message = "Items inserted successfully." });
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