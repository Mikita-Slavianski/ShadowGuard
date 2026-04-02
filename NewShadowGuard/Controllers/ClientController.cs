using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewShadowGuard.Attributes;
using NewShadowGuard.Data;
using NewShadowGuard.Models;
using NewShadowGuard.Models.ViewModels;
using NewShadowGuard.Services;

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

            // Получаем информацию о тенанте
            var tenant = await _context.Tenants.FindAsync(tenantId.Value);

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
                    .ToListAsync(),

                // ← ДОБАВЬТЕ ЭТИ СТРОКИ
                CurrentPlan = tenant?.Subscription ?? "Unknown",
                SubscriptionExpiresAt = tenant?.SubscriptionExpiresAt,
                Status = tenant?.Status ?? "Unknown"
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
                return RedirectToAction("NoTenant");
            }

            var tenant = await _context.Tenants.FindAsync(tenantId.Value);
            ViewBag.CurrentTenantId = tenantId.Value;
            ViewBag.Tenant = tenant;

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

        // Страница управления подпиской
        public async Task<IActionResult> Subscription()
        {
            var tenantId = GetCurrentUserTenantId();

            if (!tenantId.HasValue)
            {
                return RedirectToAction("NoTenant");
            }

            var tenant = await _context.Tenants.FindAsync(tenantId.Value);
            if (tenant == null)
            {
                return NotFound();
            }

            var model = new SubscriptionViewModel
            {
                TenantId = tenant.TenantId,
                TenantName = tenant.Name,
                CurrentPlan = tenant.Subscription,
                ExpiresAt = tenant.SubscriptionExpiresAt,
                Status = tenant.Status
            };

            return View(model);
        }

        // Продление подписки
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExtendSubscription(int months = 12)
        {
            var tenantId = GetCurrentUserTenantId();

            if (!tenantId.HasValue)
            {
                return RedirectToAction("NoTenant");
            }

            var tenant = await _context.Tenants.FindAsync(tenantId.Value);
            if (tenant != null)
            {
                // Отсчитываем от текущей даты или от даты истечения
                var fromDate = tenant.SubscriptionExpiresAt.HasValue &&
                               tenant.SubscriptionExpiresAt.Value > DateTime.UtcNow
                               ? tenant.SubscriptionExpiresAt.Value
                               : DateTime.UtcNow;

                tenant.SubscriptionExpiresAt = fromDate.AddMonths(months);
                await _context.SaveChangesAsync();

                var userId = GetCurrentUserUserId();
                await LogAuditAction(userId.Value, "ExtendSubscription", "Tenant", tenantId.Value,
                    $"Подписка продлена на {months} мес. до {tenant.SubscriptionExpiresAt:dd.MM.yyyy}");

                TempData["Success"] = $"✅ Подписка продлена на {months} мес. до {tenant.SubscriptionExpiresAt:dd.MM.yyyy}";
            }
            return RedirectToAction("Subscription");
        }

        // Смена тарифного плана
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePlan(string newPlan)
        {
            var tenantId = GetCurrentUserTenantId();

            if (!tenantId.HasValue)
            {
                return RedirectToAction("NoTenant");
            }

            var validPlans = new[] { "Basic", "Professional", "Enterprise" };
            if (!validPlans.Contains(newPlan))
            {
                TempData["Error"] = "❌ Неверный тарифный план";
                return RedirectToAction("Subscription");
            }

            var tenant = await _context.Tenants.FindAsync(tenantId.Value);
            if (tenant != null)
            {
                var oldPlan = tenant.Subscription;
                tenant.Subscription = newPlan;
                await _context.SaveChangesAsync();

                var userId = GetCurrentUserUserId();
                await LogAuditAction(userId.Value, "ChangePlan", "Tenant", tenantId.Value,
                    $"Тариф изменён: {oldPlan} → {newPlan}");

                TempData["Success"] = $"✅ Тарифный план изменён на {newPlan}";
            }
            return RedirectToAction("Subscription");
        }

        // Отмена подписки
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSubscription()
        {
            var tenantId = GetCurrentUserTenantId();

            if (!tenantId.HasValue)
            {
                return RedirectToAction("NoTenant");
            }

            var tenant = await _context.Tenants.FindAsync(tenantId.Value);
            if (tenant != null)
            {
                tenant.Status = "Blocked";
                await _context.SaveChangesAsync();

                var userId = GetCurrentUserUserId();
                await LogAuditAction(userId.Value, "CancelSubscription", "Tenant", tenantId.Value,
                    $"Подписка отменена. Тенант заблокирован.");

                TempData["Warning"] = "⚠️ Подписка отменена. Доступ к системе будет ограничен.";

                // Выход из системы
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }
            return RedirectToAction("Subscription");
        }

        // Обработка оплаты (имитация)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(PaymentViewModel model)
        {
            var tenantId = GetCurrentUserTenantId();

            if (!tenantId.HasValue)
            {
                TempData["Error"] = "❌ Ошибка: тенант не найден";
                return RedirectToAction("Subscription");
            }

            // Серверная валидация
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "❌ Ошибка валидации платёжных данных";
                return RedirectToAction("Subscription");
            }

            // Дополнительная проверка срока действия карты
            if (!ValidateExpiryDate(model.ExpiryDate, out string errorMessage))
            {
                TempData["Error"] = $"❌ {errorMessage}";
                return RedirectToAction("Subscription");
            }

            // Дополнительная проверка номера карты (алгоритм Луна)
            if (!ValidateCardNumber(model.CardNumber))
            {
                TempData["Error"] = "❌ Неверный номер карты";
                return RedirectToAction("Subscription");
            }

            // Имитация обработки платежа
            await Task.Delay(1000);

            var tenant = await _context.Tenants.FindAsync(tenantId.Value);
            if (tenant != null)
            {
                // Обновляем план
                tenant.Subscription = model.Plan;

                // Обновляем дату истечения
                var fromDate = tenant.SubscriptionExpiresAt.HasValue &&
                               tenant.SubscriptionExpiresAt.Value > DateTime.UtcNow
                               ? tenant.SubscriptionExpiresAt.Value
                               : DateTime.UtcNow;

                tenant.SubscriptionExpiresAt = fromDate.AddMonths(model.Months);
                tenant.Status = "Active";

                await _context.SaveChangesAsync();

                var userId = GetCurrentUserUserId();
                await LogAuditAction(userId.Value, "PaymentProcessed", "Tenant", tenantId.Value,
                    $"Оплата обработана. План: {model.Plan}, Срок: {model.Months} мес., Карта: ****{model.CardNumber.Substring(model.CardNumber.Length - 4)}");

                TempData["Success"] = $"✅ Оплата успешна! План: {model.Plan}, Продлено на {model.Months} мес.";
            }
            return RedirectToAction("Subscription");
        }

        // Валидация срока действия карты
        private bool ValidateExpiryDate(string expiryDate, out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrEmpty(expiryDate))
            {
                errorMessage = "Срок действия не указан";
                return false;
            }

            try
            {
                var parts = expiryDate.Split('/');
                if (parts.Length != 2)
                {
                    errorMessage = "Неверный формат срока действия";
                    return false;
                }

                int month = int.Parse(parts[0]);
                int year = int.Parse(parts[1]) + 2000;

                if (month < 1 || month > 12)
                {
                    errorMessage = "Неверный месяц";
                    return false;
                }

                var expiry = new DateTime(year, month, DateTime.DaysInMonth(year, month));

                if (expiry < DateTime.UtcNow)
                {
                    errorMessage = "Срок действия карты истёк";
                    return false;
                }

                return true;
            }
            catch
            {
                errorMessage = "Неверный формат срока действия";
                return false;
            }
        }

        // Валидация номера карты (алгоритм Луна)
        private bool ValidateCardNumber(string cardNumber)
        {
            // Для учебного проекта принимаем любые номера (имитация)
            // В реальном проекте раскомментируйте код ниже:


            var digits = new List<int>();
            foreach (char c in cardNumber)
            {
                if (char.IsDigit(c))
                    digits.Add(c - '0');
            }

            if (digits.Count < 13 || digits.Count > 19)
                return false;

            int sum = 0;
            bool alternate = false;
            for (int i = digits.Count - 1; i >= 0; i--)
            {
                int n = digits[i];
                if (alternate)
                {
                    n *= 2;
                    if (n > 9) n -= 9;
                }
                sum += n;
                alternate = !alternate;
            }

            return (sum % 10 == 0);

            // Для учебного проекта всегда возвращаем true
            //return true;
        }
    }

    public class ClientDashboardViewModel
    {
        public int TotalIncidents { get; set; }
        public int NewIncidents { get; set; }
        public int CriticalIncidents { get; set; }
        public int TotalAssets { get; set; }
        public List<Incident> RecentIncidents { get; set; }

        public string CurrentPlan { get; set; }
        public DateTime? SubscriptionExpiresAt { get; set; }
        public string Status { get; set; }

        public int DaysUntilExpiry => SubscriptionExpiresAt.HasValue
            ? (int)(SubscriptionExpiresAt.Value - DateTime.UtcNow).TotalDays
            : 0;

        public string PlanStatus => DaysUntilExpiry <= 0 ? "Истекла"
            : DaysUntilExpiry <= 30 ? "Истекает скоро"
            : "Активна";
    }

    // ViewModel для подписки
    public class SubscriptionViewModel
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; }
        public string CurrentPlan { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string Status { get; set; }

        // Для расчёта дней до истечения
        public int DaysUntilExpiry => ExpiresAt.HasValue
            ? (int)(ExpiresAt.Value - DateTime.UtcNow).TotalDays
            : 0;

        public string PlanStatus => DaysUntilExpiry <= 0 ? "Истекла"
            : DaysUntilExpiry <= 30 ? "Истекает скоро"
            : "Активна";
    }
}