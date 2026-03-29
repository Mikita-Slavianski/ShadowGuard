using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewShadowGuard.Data;
using NewShadowGuard.Models;
using NewShadowGuard.Models.ViewModels;

namespace NewShadowGuard.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Страница входа
        public IActionResult Login()
        {
            // Если уже авторизован - перенаправляем на дашборд
            if (HttpContext.Session.GetString("UserId") != null)
            {
                var role = HttpContext.Session.GetString("Role");
                return RedirectToAction("Index", GetDashboardByRole(role));
            }

            return View();
        }

        // Обработка входа
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _context.Users
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ModelState.AddModelError("", "Неверный email или пароль");
                return View();
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Учётная запись заблокирована");
                return View();
            }

            // Проверка пароля через BCrypt
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Неверный email или пароль");
                return View();
            }

            // Сохраняем данные пользователя в сессии
            HttpContext.Session.SetString("UserId", user.UserId.ToString());
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetString("FullName", user.FullName);
            HttpContext.Session.SetString("Role", user.Role);

            if (user.TenantId.HasValue)
            {
                HttpContext.Session.SetString("TenantId", user.TenantId.Value.ToString());
                HttpContext.Session.SetString("TenantName", user.Tenant.Name);
            }

            // Логируем вход в AuditLog
            await LogAuditAction(user.UserId, "Login", "User", user.UserId);

            // Перенаправляем на дашборд по роли
            return RedirectToAction("Index", GetDashboardByRole(user.Role));
        }

        // Страница смены пароля
        public IActionResult ChangePassword()
        {
            // Проверяем, авторизован ли пользователь
            if (HttpContext.Session.GetString("UserId") == null)
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Получаем пользователя из БД
            var user = await _context.Users.FindAsync(int.Parse(userId));

            if (user == null)
            {
                ModelState.AddModelError("", "Пользователь не найден");
                return View(model);
            }

            // Проверяем старый пароль
            if (!BCrypt.Net.BCrypt.Verify(model.OldPassword, user.PasswordHash))
            {
                ModelState.AddModelError("OldPassword", "Неверный текущий пароль");
                return View(model);
            }

            // Проверяем, что новый пароль не совпадает со старым
            if (BCrypt.Net.BCrypt.Verify(model.NewPassword, user.PasswordHash))
            {
                ModelState.AddModelError("NewPassword", "Новый пароль должен отличаться от текущего");
                return View(model);
            }

            // Обновляем пароль
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            // Логируем смену пароля
            await LogAuditAction(int.Parse(userId), "ChangePassword", "User", user.UserId);

            TempData["Success"] = "✅ Пароль успешно изменён!";
            return RedirectToAction("ChangePassword");
        }

        // Выход
        public async Task<IActionResult> Logout()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(userId))
            {
                await LogAuditAction(int.Parse(userId), "Logout", "User", int.Parse(userId));
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // Определение контроллера дашборда по роли
        private string GetDashboardByRole(string role)
        {
            return role switch
            {
                "Admin" => "Admin",
                "Analyst" => "Analyst",
                "Client" => "Client",
                _ => "Account"
            };
        }

        // Запись в AuditLog
        private async Task LogAuditAction(int userId, string action, string entityType, int? entityId)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}