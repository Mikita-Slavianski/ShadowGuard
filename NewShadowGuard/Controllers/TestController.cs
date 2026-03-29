using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewShadowGuard.Data;

namespace NewShadowGuard.Controllers
{
    public class TestController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TestController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var result = new TestViewModel();

            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                result.ConnectionStatus = canConnect ? "✅ Подключено" : "❌ Ошибка подключения";

                result.TenantCount = await _context.Tenants.CountAsync();
                result.UserCount = await _context.Users.CountAsync();
                result.AssetCount = await _context.Assets.CountAsync();
                result.LogCount = await _context.Logs.CountAsync();
                result.IncidentCount = await _context.Incidents.CountAsync();

                result.Users = await _context.Users
                    .Include(u => u.Tenant)
                    .Take(10)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                result.ConnectionStatus = $"❌ Ошибка: {ex.Message}";
            }

            return View(result);
        }
    }

    public class TestViewModel
    {
        public string ConnectionStatus { get; set; }
        public int TenantCount { get; set; }
        public int UserCount { get; set; }
        public int AssetCount { get; set; }
        public int LogCount { get; set; }
        public int IncidentCount { get; set; }
        public List<Models.User> Users { get; set; }
    }
}