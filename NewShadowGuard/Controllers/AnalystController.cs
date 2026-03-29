using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewShadowGuard.Attributes;
using NewShadowGuard.Data;
using NewShadowGuard.Models;

namespace CyberSecurityApp.Controllers
{
    [CustomAuthorize("Analyst", "Admin")]
    public class AnalystController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnalystController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Главный дашборд аналитика
        public async Task<IActionResult> Index()
        {
            var tenantId = GetCurrentUserTenantId();

            var model = new AnalystDashboardViewModel
            {
                TotalIncidents = await GetIncidentCount(tenantId),
                NewIncidents = await GetIncidentCount(tenantId, "New"),
                InProgressIncidents = await GetIncidentCount(tenantId, "InProgress"),
                ResolvedIncidents = await GetIncidentCount(tenantId, "Resolved"),
                CriticalIncidents = await GetIncidentCount(tenantId, null, "Critical"),
                RecentIncidents = await GetIncidents(tenantId, 10)
            };

            return View(model);
        }

        #region Инциденты

        public async Task<IActionResult> Incidents(string? status, string? severity)
        {
            var tenantId = GetCurrentUserTenantId();
            var isAdmin = IsAdmin();

            var query = _context.Incidents
                .Include(i => i.Log)
                .Include(i => i.Tenant)
                .AsQueryable();

            // Админ видит все, аналитик только своего тенанта
            if (!isAdmin && tenantId.HasValue)
            {
                query = query.Where(i => i.TenantId == tenantId);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(i => i.Status == status);
            }

            if (!string.IsNullOrEmpty(severity))
            {
                query = query.Where(i => i.Severity == severity);
            }

            var incidents = await query
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            ViewBag.Status = status;
            ViewBag.Severity = severity;

            return View(incidents);
        }

        public async Task<IActionResult> IncidentDetails(int id)
        {
            var incident = await _context.Incidents
                .Include(i => i.Log)
                .Include(i => i.Tenant)
                .Include(i => i.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(i => i.IncidentId == id);

            if (incident == null) return NotFound();

            // Проверка доступа
            if (!IsAdmin())
            {
                var tenantId = GetCurrentUserTenantId();
                if (tenantId.HasValue && incident.TenantId != tenantId)
                {
                    return RedirectToAction("AccessDenied", "Account");
                }
            }

            return View(incident);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateIncidentStatus(int incidentId, string status)
        {
            var incident = await _context.Incidents.FindAsync(incidentId);
            if (incident != null)
            {
                incident.Status = status;
                incident.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await LogAuditAction("UpdateStatus", "Incident", incidentId,
                    $"Статус изменён на: {status}");
                TempData["Success"] = "Статус инцидента обновлён";
            }
            return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int incidentId, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var userId = GetCurrentUserUserId();
                var comment = new Comment
                {
                    IncidentId = incidentId,
                    UserId = userId.Value,
                    Text = text,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();
                await LogAuditAction("AddComment", "Comment", comment.CommentId,
                    $"Комментарий к инциденту {incidentId}");
            }
            return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
        }

        #endregion

        #region Активы

        public async Task<IActionResult> Assets()
        {
            var tenantId = GetCurrentUserTenantId();
            var isAdmin = IsAdmin();

            var query = _context.Assets
                .Include(a => a.Tenant)
                .AsQueryable();

            if (!isAdmin && tenantId.HasValue)
            {
                query = query.Where(a => a.TenantId == tenantId);
            }

            var assets = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
            return View(assets);
        }

        public async Task<IActionResult> CreateAsset()
        {
            ViewBag.Tenants = await _context.Tenants.Where(t => t.Status == "Active").ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsset(Asset asset)
        {
            if (ModelState.IsValid)
            {
                // Если не админ, привязываем к тенанту аналитика
                if (!IsAdmin())
                {
                    asset.TenantId = GetCurrentUserTenantId();
                }

                asset.CreatedAt = DateTime.UtcNow;
                _context.Assets.Add(asset);
                await _context.SaveChangesAsync();
                await LogAuditAction("Create", "Asset", asset.AssetId,
                    $"Создан актив: {asset.Name}");
                TempData["Success"] = "Актив успешно создан";
                return RedirectToAction(nameof(Assets));
            }
            ViewBag.Tenants = await _context.Tenants.Where(t => t.Status == "Active").ToListAsync();
            return View(asset);
        }

        public async Task<IActionResult> EditAsset(int id)
        {
            var asset = await _context.Assets.FindAsync(id);
            if (asset == null) return NotFound();
            ViewBag.Tenants = await _context.Tenants.Where(t => t.Status == "Active").ToListAsync();
            return View(asset);
        }

        [HttpPost]
        public async Task<IActionResult> EditAsset(Asset asset)
        {
            if (ModelState.IsValid)
            {
                var existing = await _context.Assets.FindAsync(asset.AssetId);
                if (existing != null)
                {
                    existing.Name = asset.Name;
                    existing.Type = asset.Type;
                    existing.IpAddress = asset.IpAddress;
                    existing.Os = asset.Os;
                    existing.Criticality = asset.Criticality;
                    await _context.SaveChangesAsync();
                    await LogAuditAction("Update", "Asset", asset.AssetId,
                        $"Обновлён актив: {asset.Name}");
                    TempData["Success"] = "Актив успешно обновлён";
                }
                return RedirectToAction(nameof(Assets));
            }
            ViewBag.Tenants = await _context.Tenants.Where(t => t.Status == "Active").ToListAsync();
            return View(asset);
        }

        #endregion

        #region Логи

        public async Task<IActionResult> Logs(int? assetId)
        {
            var query = _context.Logs
                .Include(l => l.Asset)
                .AsQueryable();

            if (assetId.HasValue)
            {
                query = query.Where(l => l.AssetId == assetId);
            }

            var logs = await query.OrderByDescending(l => l.Timestamp).Take(100).ToListAsync();
            ViewBag.AssetId = assetId;
            return View(logs);
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

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("Role");
            return role == "Admin";
        }

        private async Task<int> GetIncidentCount(int? tenantId, string? status = null, string? severity = null)
        {
            var query = _context.Incidents.AsQueryable();

            if (!IsAdmin() && tenantId.HasValue)
            {
                query = query.Where(i => i.TenantId == tenantId);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(i => i.Status == status);
            }

            if (!string.IsNullOrEmpty(severity))
            {
                query = query.Where(i => i.Severity == severity);
            }

            return await query.CountAsync();
        }

        private async Task<List<Incident>> GetIncidents(int? tenantId, int take)
        {
            var query = _context.Incidents
                .Include(i => i.Tenant)
                .AsQueryable();

            if (!IsAdmin() && tenantId.HasValue)
            {
                query = query.Where(i => i.TenantId == tenantId);
            }

            return await query.OrderByDescending(i => i.CreatedAt).Take(take).ToListAsync();
        }

        private async Task LogAuditAction(string action, string entityType, int? entityId, string details)
        {
            var userId = GetCurrentUserUserId();
            if (userId.HasValue)
            {
                var auditLog = new AuditLog
                {
                    UserId = userId.Value,
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
    }

    public class AnalystDashboardViewModel
    {
        public int TotalIncidents { get; set; }
        public int NewIncidents { get; set; }
        public int InProgressIncidents { get; set; }
        public int ResolvedIncidents { get; set; }
        public int CriticalIncidents { get; set; }
        public List<Incident> RecentIncidents { get; set; }
    }
}