using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewShadowGuard.Attributes;
using NewShadowGuard.Data;
using NewShadowGuard.Models;

namespace NewShadowGuard.Controllers
{
    [CustomAuthorize("Client")]
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClientController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Главный дашборд клиента
        public async Task<IActionResult> Index()
        {
            var tenantId = GetCurrentUserTenantId();

            // Если у пользователя нет тенанта - перенаправляем на специальную страницу
            if (!tenantId.HasValue)
            {
                return RedirectToAction("NoTenant");
            }

            var model = new ClientDashboardViewModel
            {
                TotalIncidents = await _context.Incidents.CountAsync(i => i.TenantId == tenantId),
                NewIncidents = await _context.Incidents.CountAsync(i => i.TenantId == tenantId && i.Status == "New"),
                CriticalIncidents = await _context.Incidents.CountAsync(i => i.TenantId == tenantId && i.Severity == "Critical"),
                TotalAssets = await _context.Assets.CountAsync(a => a.TenantId == tenantId),
                RecentIncidents = await _context.Incidents
                    .Where(i => i.TenantId == tenantId)
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(5)
                    .ToListAsync()
            };

            return View(model);
        }

        // Новая страница: Нет тенанта
        public IActionResult NoTenant()
        {
            return View();
        }

        // Метод создания тенанта (если пользователь хочет создать сам)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTenantFromNoTenant(string companyName, string country = "Россия")
        {
            var userId = GetCurrentUserUserId();

            if (!string.IsNullOrEmpty(companyName) && userId.HasValue)
            {
                // Проверяем, нет ли уже тенанта у пользователя
                var user = await _context.Users.FindAsync(userId.Value);
                if (user != null && user.TenantId == null)
                {
                    // Создаём новый тенант
                    var tenant = new Tenant
                    {
                        Name = companyName,
                        Country = country,
                        Subscription = "Basic",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow,
                        SubscriptionExpiresAt = DateTime.UtcNow.AddMonths(1) // Пробный период
                    };

                    _context.Tenants.Add(tenant);
                    await _context.SaveChangesAsync();

                    // Привязываем пользователя к тенанту
                    user.TenantId = tenant.TenantId;
                    await _context.SaveChangesAsync();

                    // Обновляем сессию
                    HttpContext.Session.SetString("TenantId", tenant.TenantId.ToString());
                    HttpContext.Session.SetString("TenantName", tenant.Name);

                    // Логируем
                    await LogAuditAction(userId.Value, "Create", "Tenant", tenant.TenantId,
                        $"Создан тенант через страницу NoTenant: {tenant.Name}");

                    TempData["Success"] = "✅ Тенант успешно создан! Теперь у вас есть доступ ко всем функциям.";
                    return RedirectToAction("Index");
                }
            }

            return RedirectToAction("NoTenant");
        }

        #region Инциденты (только чтение)

        public async Task<IActionResult> Incidents(string? status)
        {
            var tenantId = GetCurrentUserTenantId();
            if (!tenantId.HasValue)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var query = _context.Incidents
                .Include(i => i.Log)
                .Where(i => i.TenantId == tenantId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(i => i.Status == status);
            }

            var incidents = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
            ViewBag.Status = status;
            return View(incidents);
        }

        public async Task<IActionResult> IncidentDetails(int id)
        {
            var tenantId = GetCurrentUserTenantId();
            var incident = await _context.Incidents
                .Include(i => i.Log)
                .Include(i => i.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(i => i.IncidentId == id);

            if (incident == null || incident.TenantId != tenantId)
            {
                return NotFound();
            }

            return View(incident);
        }

        #endregion

        #region Активы (только чтение)

        public async Task<IActionResult> Assets()
        {
            var tenantId = GetCurrentUserTenantId();
            if (!tenantId.HasValue)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var assets = await _context.Assets
                .Where(a => a.TenantId == tenantId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(assets);
        }

        #endregion

        #region Вспомогательные методы

        private int? GetCurrentUserTenantId()
        {
            var tenantId = HttpContext.Session.GetString("TenantId");
            return string.IsNullOrEmpty(tenantId) ? null : int.Parse(tenantId);
        }

        private int? GetCurrentUserUserId()
        {
            var userId = HttpContext.Session.GetString("UserId");
            return string.IsNullOrEmpty(userId) ? null : int.Parse(userId);
        }

        private async Task LogAuditAction(int userId, string action, string entityType, int? entityId, string details)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                NewValue = details,
                Timestamp = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        #endregion
    }

    public class ClientDashboardViewModel
    {
        public int TotalIncidents { get; set; }
        public int NewIncidents { get; set; }
        public int CriticalIncidents { get; set; }
        public int TotalAssets { get; set; }
        public List<Incident> RecentIncidents { get; set; }
    }
}