using Microsoft.AspNetCore.Mvc;
using NewShadowGuard.Attributes;

namespace CyberSecurityApp.Controllers
{
    public class HomeController : Controller
    {
        [CustomAuthorize]
        public IActionResult Index()
        {
            var role = HttpContext.Session.GetString("Role");
            return RedirectToAction("Index", role == "Admin" ? "Admin" :
                                                   role == "Analyst" ? "Analyst" : "Client");
        }

        public IActionResult About()
        {
            return View();
        }
    }
}