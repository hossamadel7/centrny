using Microsoft.AspNetCore.Mvc;

namespace centrny.Controllers
{
    public class WebsiteController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
