using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewShadowGuard.Attributes;
using NewShadowGuard.Data;
using NewShadowGuard.Models;
using NewShadowGuard.Services;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace NewShadowGuard.Controllers
{
    [CustomAuthorize("Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Главный дашборд админа
        public async Task<IActionResult> Index()
        {
            var model = new AdminDashboardViewModel
            {
                TenantCount = await _context.Tenants.CountAsync(),
                UserCount = await _context.Users.CountAsync(),
                AssetCount = await _context.Assets.CountAsync(),
                IncidentCount = await _context.Incidents.CountAsync(),
                ActiveIncidents = await _context.Incidents.CountAsync(i => i.Status == "New" || i.Status == "InProgress"),

                // ← ДОБАВЬТЕ ЭТИ СТРОКИ
                NewIncidents = await _context.Incidents.CountAsync(i => i.Status == "New"),
                InProgressIncidents = await _context.Incidents.CountAsync(i => i.Status == "InProgress"),
                ResolvedIncidents = await _context.Incidents.CountAsync(i => i.Status == "Resolved"),
                CriticalIncidents = await _context.Incidents.CountAsync(i => i.Severity == "Critical"),

                RecentTenants = await _context.Tenants.OrderByDescending(t => t.CreatedAt).Take(5).ToListAsync(),
                RecentUsers = await _context.Users.Include(u => u.Tenant).OrderByDescending(u => u.CreatedAt).Take(5).ToListAsync()
            };

            return View(model);
        }

        #region Тенанты

        public async Task<IActionResult> Tenants()
        {
            var tenants = await _context.Tenants.OrderByDescending(t => t.CreatedAt).ToListAsync();
            return View(tenants);
        }

        public IActionResult CreateTenant()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateTenant(Tenant tenant)
        {
            if (ModelState.IsValid)
            {
                tenant.CreatedAt = DateTime.UtcNow;
                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();
                await LogAuditAction("Create", "Tenant", tenant.TenantId, $"Создан тенант: {tenant.Name}");
                TempData["Success"] = "Тенант успешно создан";
                return RedirectToAction(nameof(Tenants));
            }
            return View(tenant);
        }

        public async Task<IActionResult> EditTenant(int id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();
            return View(tenant);
        }

        [HttpPost]
        public async Task<IActionResult> EditTenant(Tenant tenant)
        {
            if (ModelState.IsValid)
            {
                var existing = await _context.Tenants.FindAsync(tenant.TenantId);
                if (existing == null) return NotFound();

                existing.Name = tenant.Name;
                existing.Country = tenant.Country;
                existing.Subscription = tenant.Subscription;
                existing.Status = tenant.Status;
                existing.SubscriptionExpiresAt = tenant.SubscriptionExpiresAt;  // ← Убедитесь, что эта строка есть!

                await _context.SaveChangesAsync();
                await LogAuditAction("Update", "Tenant", tenant.TenantId, $"Обновлён тенант: {tenant.Name}");
                TempData["Success"] = "Тенант успешно обновлён";
                return RedirectToAction(nameof(Tenants));
            }
            return View(tenant);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTenant(int id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant != null)
            {
                _context.Tenants.Remove(tenant);
                await _context.SaveChangesAsync();
                await LogAuditAction("Delete", "Tenant", id, $"Удалён тенант: {tenant.Name}");
                TempData["Success"] = "Тенант успешно удалён";
            }
            return RedirectToAction(nameof(Tenants));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExtendSubscription(int tenantId, int months = 12)
        {
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant != null)
            {
                // Если подписка уже истекла, отсчитываем от сегодня
                // Если ещё активна, отсчитываем от даты истечения
                var fromDate = tenant.SubscriptionExpiresAt.HasValue &&
                               tenant.SubscriptionExpiresAt.Value > DateTime.UtcNow
                               ? tenant.SubscriptionExpiresAt.Value
                               : DateTime.UtcNow;

                tenant.SubscriptionExpiresAt = fromDate.AddMonths(months);
                await _context.SaveChangesAsync();

                await LogAuditAction("ExtendSubscription", "Tenant", tenantId,
                    $"Подписка продлена на {months} мес. до {tenant.SubscriptionExpiresAt:dd.MM.yyyy}");

                TempData["Success"] = $"Подписка продлена до {tenant.SubscriptionExpiresAt:dd.MM.yyyy}";
            }
            return RedirectToAction(nameof(Tenants));
        }

        #endregion

        #region Пользователи

        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .Include(u => u.Tenant)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
            return View(users);
        }

        public async Task<IActionResult> CreateUser()
        {
            ViewBag.Tenants = await _context.Tenants
                .Where(t => t.Status == "Active")
                .OrderBy(t => t.Name)
                .ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(User user)
        {
            ViewBag.Tenants = await _context.Tenants
                .Where(t => t.Status == "Active")
                .OrderBy(t => t.Name)
                .ToListAsync();

            // Проверка на уникальность Email
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == user.Email);

            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "Пользователь с таким Email уже существует");
                return View(user);
            }

            // Проверка обязательных полей
            if (string.IsNullOrEmpty(user.Email))
            {
                ModelState.AddModelError("Email", "Email обязателен");
            }
            if (string.IsNullOrEmpty(user.FullName))
            {
                ModelState.AddModelError("FullName", "Имя обязательно");
            }
            if (string.IsNullOrEmpty(user.Role))
            {
                ModelState.AddModelError("Role", "Роль обязательна");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Генерируем хеш для пароля "password123"
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");
                    user.CreatedAt = DateTime.UtcNow;
                    user.IsActive = true;
                    user.MfaEnabled = user.MfaEnabled;

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    await LogAuditAction("Create", "User", user.UserId,
                        $"Создан пользователь: {user.Email} (Роль: {user.Role})");

                    TempData["Success"] = $"Пользователь {user.Email} успешно создан! Пароль: password123";
                    return RedirectToAction(nameof(Users));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Ошибка при создании: {ex.Message}");
                    return View(user);
                }
            }

            return View(user);
        }

        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _context.Users
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return NotFound();

            ViewBag.Tenants = await _context.Tenants
                .Where(t => t.Status == "Active")
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User user)
        {
            ViewBag.Tenants = await _context.Tenants
                .Where(t => t.Status == "Active")
                .OrderBy(t => t.Name)
                .ToListAsync();

            // Проверка на уникальность Email (исключая текущего пользователя)
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == user.Email && u.UserId != user.UserId);

            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "Пользователь с таким Email уже существует");
                return View(user);
            }

            // Проверка обязательных полей
            if (string.IsNullOrEmpty(user.Email))
            {
                ModelState.AddModelError("Email", "Email обязателен");
            }
            if (string.IsNullOrEmpty(user.FullName))
            {
                ModelState.AddModelError("FullName", "Имя обязательно");
            }
            if (string.IsNullOrEmpty(user.Role))
            {
                ModelState.AddModelError("Role", "Роль обязательна");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Users.FindAsync(user.UserId);
                    if (existing == null)
                        return NotFound();

                    existing.Email = user.Email;
                    existing.FullName = user.FullName;
                    existing.Role = user.Role;
                    existing.TenantId = user.TenantId;
                    existing.MfaEnabled = user.MfaEnabled;
                    existing.IsActive = user.IsActive;

                    await _context.SaveChangesAsync();

                    await LogAuditAction("Update", "User", user.UserId,
                        $"Обновлён пользователь: {user.Email}");

                    TempData["Success"] = "Пользователь успешно обновлён";
                    return RedirectToAction(nameof(Users));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Ошибка при обновлении: {ex.Message}");
                    return View(user);
                }
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");
                await _context.SaveChangesAsync();
                await LogAuditAction("ResetPassword", "User", id,
                    $"Сброшен пароль: {user.Email}");
                TempData["Success"] = "Пароль сброшен на 'password123'";
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                try
                {
                    var userEmail = user.Email;
                    _context.Users.Remove(user);
                    await _context.SaveChangesAsync();
                    await LogAuditAction("Delete", "User", id,
                        $"Удалён пользователь: {userEmail}");
                    TempData["Success"] = "Пользователь успешно удалён";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Ошибка при удалении: {ex.Message}";
                }
            }
            return RedirectToAction(nameof(Users));
        }

        #endregion

        #region Аудит

        public async Task<IActionResult> AuditLog(string? entityType, int? entityId)
        {
            try
            {
                var query = _context.AuditLogs
                    .Include(a => a.User)
                    .OrderByDescending(a => a.Timestamp)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(entityType))
                {
                    query = query.Where(a => a.EntityType == entityType);
                }

                var logs = await query.Take(100).ToListAsync();
                ViewBag.EntityType = entityType;
                return View(logs);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ошибка при загрузке журнала: {ex.Message}";
                return View(new List<AuditLog>());
            }
        }

        #endregion

        #region Вспомогательные методы

        private async Task LogAuditAction(string action, string entityType, int? entityId, string details)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(userId))
            {
                var auditLog = new AuditLog
                {
                    UserId = int.Parse(userId),
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    NewValue = details,
                    Timestamp = DateTime.UtcNow
                };
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
        }

        #endregion

        #region Экспорт и очистка аудита

        public async Task<IActionResult> ExportAuditLog(string format = "excel")
        {
            var logs = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(1000)
                .Select(a => new AuditLogExportDto
                {
                    Timestamp = a.Timestamp,
                    UserName = a.User != null ? a.User.FullName : "System",
                    Action = a.Action,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    Details = a.NewValue
                })
                .ToListAsync();

            var exportService = new ExportService();

            if (format.ToLower() == "excel")
            {
                var fileBytes = exportService.ExportToExcel(logs, "AuditLog");
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"AuditLog_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            else if (format.ToLower() == "csv")
            {
                var fileBytes = exportService.ExportToCsv(logs);
                return File(fileBytes, "text/csv", $"AuditLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }

            return RedirectToAction(nameof(AuditLog));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAuditLog()
        {
            var userId = GetCurrentUserUserId();

            // Удаляем все записи аудита
            var logs = await _context.AuditLogs.ToListAsync();
            _context.AuditLogs.RemoveRange(logs);
            await _context.SaveChangesAsync();

            // Логируем очистку
            await LogAuditAction("Clear", "AuditLog", null,
                $"Журнал аудита очищен. Удалено записей: {logs.Count}");

            TempData["Success"] = $"Журнал аудита очищен. Удалено {logs.Count} записей.";
            return RedirectToAction(nameof(AuditLog));
        }

        #endregion

        #region Вспомогательные методы

        private int? GetCurrentUserUserId()
        {
            var userId = HttpContext.Session.GetString("UserId");
            return string.IsNullOrEmpty(userId) ? null : int.Parse(userId);
        }

        #endregion
    }



    // ViewModel для дашборда
    public class AdminDashboardViewModel
    {
        public int TenantCount { get; set; }
        public int UserCount { get; set; }
        public int AssetCount { get; set; }
        public int IncidentCount { get; set; }
        public int ActiveIncidents { get; set; }

        // ← ДОБАВЬТЕ ЭТИ СВОЙСТВА
        public int NewIncidents { get; set; }
        public int InProgressIncidents { get; set; }
        public int ResolvedIncidents { get; set; }
        public int CriticalIncidents { get; set; }

        public List<Tenant> RecentTenants { get; set; }
        public List<User> RecentUsers { get; set; }
    }

    public class AuditLogExportDto
    {
        public DateTime Timestamp { get; set; }
        public string UserName { get; set; }
        public string Action { get; set; }
        public string EntityType { get; set; }
        public int? EntityId { get; set; }
        public string Details { get; set; }
    }
}