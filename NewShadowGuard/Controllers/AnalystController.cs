using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewShadowGuard.Attributes;
using NewShadowGuard.Data;
using NewShadowGuard.Models;
using NewShadowGuard.Services;

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

        // Обновлённый метод IncidentDetails (добавьте загрузку тегов)
        public async Task<IActionResult> IncidentDetails(int id)
        {
            var incident = await _context.Incidents
                .Include(i => i.Log)
                .Include(i => i.Tenant)
                .Include(i => i.Comments)
                    .ThenInclude(c => c.User)
                .Include(i => i.Tags)  // ← Добавьте это свойство навигации
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

            // Загружаем теги отдельно
            var tags = await _context.IncidentTags
                .Include(t => t.CreatedByUser)
                .Where(t => t.IncidentId == id)
                .ToListAsync();

            ViewBag.Tags = tags;

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

            // Если не админ, сразу устанавливаем TenantId
            if (!IsAdmin())
            {
                var tenantId = GetCurrentUserTenantId();
                ViewBag.DefaultTenantId = tenantId;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAsset(Asset asset)
        {
            ViewBag.Tenants = await _context.Tenants.Where(t => t.Status == "Active").ToListAsync();

            // Если не админ, привязываем к тенанту аналитика
            if (!IsAdmin())
            {
                asset.TenantId = GetCurrentUserTenantId();
            }

            // Проверка на обязательные поля
            if (string.IsNullOrEmpty(asset.Name))
            {
                ModelState.AddModelError("Name", "Название обязательно");
            }
            if (string.IsNullOrEmpty(asset.Type))
            {
                ModelState.AddModelError("Type", "Тип обязателен");
            }
            if (string.IsNullOrEmpty(asset.Criticality))
            {
                ModelState.AddModelError("Criticality", "Критичность обязательна");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    asset.CreatedAt = DateTime.UtcNow;
                    _context.Assets.Add(asset);
                    await _context.SaveChangesAsync();

                    await LogAuditAction("Create", "Asset", asset.AssetId,
                        $"Создан актив: {asset.Name}");

                    TempData["Success"] = "Актив успешно создан";
                    return RedirectToAction(nameof(Assets));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Ошибка при создании: {ex.Message}");
                    return View(asset);
                }
            }

            return View(asset);
        }

        public async Task<IActionResult> EditAsset(int id)
        {
            var asset = await _context.Assets.FindAsync(id);
            if (asset == null) return NotFound();

            // Проверка доступа: аналитик может редактировать только активы своего тенанта
            if (!IsAdmin())
            {
                var tenantId = GetCurrentUserTenantId();
                if (tenantId.HasValue && asset.TenantId != tenantId)
                {
                    return RedirectToAction("AccessDenied", "Account");
                }
            }

            ViewBag.Tenants = await _context.Tenants.Where(t => t.Status == "Active").ToListAsync();
            return View(asset);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAsset(Asset asset)
        {
            ViewBag.Tenants = await _context.Tenants.Where(t => t.Status == "Active").ToListAsync();

            // Проверка доступа
            if (!IsAdmin())
            {
                var tenantId = GetCurrentUserTenantId();
                if (tenantId.HasValue && asset.TenantId != tenantId)
                {
                    return RedirectToAction("AccessDenied", "Account");
                }
            }

            // Проверка на обязательные поля
            if (string.IsNullOrEmpty(asset.Name))
            {
                ModelState.AddModelError("Name", "Название обязательно");
            }
            if (string.IsNullOrEmpty(asset.Type))
            {
                ModelState.AddModelError("Type", "Тип обязателен");
            }
            if (string.IsNullOrEmpty(asset.Criticality))
            {
                ModelState.AddModelError("Criticality", "Критичность обязательна");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Assets.FindAsync(asset.AssetId);
                    if (existing == null) return NotFound();

                    existing.Name = asset.Name;
                    existing.Type = asset.Type;
                    existing.IpAddress = asset.IpAddress;
                    existing.Os = asset.Os;
                    existing.Criticality = asset.Criticality;

                    await _context.SaveChangesAsync();

                    await LogAuditAction("Update", "Asset", asset.AssetId,
                        $"Обновлён актив: {asset.Name}");

                    TempData["Success"] = "Актив успешно обновлён";
                    return RedirectToAction(nameof(Assets));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Ошибка при обновлении: {ex.Message}");
                    return View(asset);
                }
            }

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

        #region Экспорт логов

        public async Task<IActionResult> ExportLogs(string format = "excel", int? assetId = null)
        {
            var query = _context.Logs
                .Include(l => l.Asset)
                .AsQueryable();

            if (assetId.HasValue)
            {
                query = query.Where(l => l.AssetId == assetId);
            }

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Take(1000)
                .Select(l => new LogExportDto
                {
                    Timestamp = l.Timestamp,
                    AssetName = l.Asset != null ? l.Asset.Name : "Unknown",
                    AssetType = l.Asset != null ? l.Asset.Type : "",
                    EventType = l.EventType,
                    RawData = l.RawData
                })
                .ToListAsync();

            var exportService = new ExportService();

            if (format.ToLower() == "excel")
            {
                var fileBytes = exportService.ExportToExcel(logs, "Logs");
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Logs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            else if (format.ToLower() == "csv")
            {
                var fileBytes = exportService.ExportToCsv(logs);
                return File(fileBytes, "text/csv", $"Logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }

            return RedirectToAction(nameof(Logs));
        }

        #endregion

        #region Расширенные действия с инцидентами

        // Блокировка IP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockIP(int incidentId, string ipAddress, string reason, int durationDays = 30)
        {
            if (!string.IsNullOrEmpty(ipAddress))
            {
                var userId = GetCurrentUserUserId();

                var blockedIP = new BlockedIP
                {
                    IpAddress = ipAddress,
                    Reason = reason,
                    BlockedBy = userId,
                    BlockedAt = DateTime.UtcNow,
                    ExpiresAt = durationDays > 0 ? DateTime.UtcNow.AddDays(durationDays) : null,
                    IsActive = true,
                    IncidentId = incidentId
                };

                _context.BlockedIPs.Add(blockedIP);
                await _context.SaveChangesAsync();

                await LogAuditAction("BlockIP", "BlockedIP", blockedIP.BlockedIpId,
                    $"Заблокирован IP: {ipAddress} (Инцидент: {incidentId})");

                TempData["Success"] = $"IP-адрес {ipAddress} заблокирован на {durationDays} дн.";
            }
            return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
        }

        // Разблокировка IP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockIP(int blockedIpId, int incidentId)
        {
            var blockedIP = await _context.BlockedIPs.FindAsync(blockedIpId);
            if (blockedIP != null)
            {
                blockedIP.IsActive = false;
                await _context.SaveChangesAsync();

                await LogAuditAction("UnblockIP", "BlockedIP", blockedIpId,
                    $"Разблокирован IP: {blockedIP.IpAddress}");

                TempData["Success"] = $"IP-адрес {blockedIP.IpAddress} разблокирован";
            }
            return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
        }

        // Эскалация инцидента
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EscalateIncident(int incidentId, string newSeverity)
        {
            var incident = await _context.Incidents.FindAsync(incidentId);
            if (incident != null)
            {
                var oldSeverity = incident.Severity;
                incident.Severity = newSeverity;
                incident.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await LogAuditAction("Escalate", "Incident", incidentId,
                    $"Критичность изменена: {oldSeverity} → {newSeverity}");

                TempData["Success"] = $"Критичность инцидента изменена на {newSeverity}";
            }
            return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
        }

        // Обновлённый метод AddTag
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTag(int incidentId, string tag)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                var userId = GetCurrentUserUserId();

                // Проверяем, нет ли уже такого тега
                var existingTag = await _context.IncidentTags
                    .FirstOrDefaultAsync(t => t.IncidentId == incidentId && t.TagName == tag);

                if (existingTag == null)
                {
                    var incidentTag = new IncidentTag
                    {
                        IncidentId = incidentId,
                        TagName = tag.Trim(),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userId
                    };

                    _context.IncidentTags.Add(incidentTag);
                    await _context.SaveChangesAsync();

                    await LogAuditAction("AddTag", "IncidentTag", incidentTag.IncidentTagId,
                        $"Добавлен тег '{tag}' к инциденту {incidentId}");

                    TempData["Success"] = $"Тег '{tag}' добавлен";
                }
                else
                {
                    TempData["Info"] = $"Тег '{tag}' уже существует";
                }
            }
            return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
        }

        // Удаление тега
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveTag(int incidentTagId, int incidentId)
        {
            var tag = await _context.IncidentTags.FindAsync(incidentTagId);
            if (tag != null)
            {
                _context.IncidentTags.Remove(tag);
                await _context.SaveChangesAsync();

                await LogAuditAction("RemoveTag", "IncidentTag", incidentTagId,
                    $"Удалён тег '{tag.TagName}' из инцидента {incidentId}");

                TempData["Success"] = $"Тег '{tag.TagName}' удалён";
            }
            return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
        }

        // Экспорт инцидента
        public async Task<IActionResult> ExportIncident(int id, string format = "csv")
        {
            var incident = await _context.Incidents
                .Include(i => i.Log)
                .Include(i => i.Tenant)
                .Include(i => i.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(i => i.IncidentId == id);

            if (incident == null) return NotFound();

            var exportData = new List<IncidentExportDto>
    {
        new IncidentExportDto
        {
            Field = "ID",
            Value = incident.IncidentId.ToString()
        },
        new IncidentExportDto
        {
            Field = "Название",
            Value = incident.Title
        },
        new IncidentExportDto
        {
            Field = "Описание",
            Value = incident.Description
        },
        new IncidentExportDto
        {
            Field = "Критичность",
            Value = incident.Severity
        },
        new IncidentExportDto
        {
            Field = "Статус",
            Value = incident.Status
        },
        new IncidentExportDto
        {
            Field = "Тенант",
            Value = incident.Tenant?.Name ?? "N/A"
        },
        new IncidentExportDto
        {
            Field = "Создан",
            Value = incident.CreatedAt.ToString("dd.MM.yyyy HH:mm")
        },
        new IncidentExportDto
        {
            Field = "Обновлён",
            Value = incident.UpdatedAt.ToString("dd.MM.yyyy HH:mm")
        }
    };

            var exportService = new ExportService();

            if (format.ToLower() == "excel")
            {
                var fileBytes = exportService.ExportToExcel(exportData, "Incident");
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Incident_{incident.IncidentId}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            else
            {
                var fileBytes = exportService.ExportToCsv(exportData);
                return File(fileBytes, "text/csv", $"Incident_{incident.IncidentId}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
        }

        // Список заблокированных IP
        public async Task<IActionResult> BlockedIPs()
        {
            var blockedIPs = await _context.BlockedIPs
                .Include(b => b.BlockedByUser)
                .Include(b => b.Incident)
                .OrderByDescending(b => b.BlockedAt)
                .ToListAsync();

            return View(blockedIPs);
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

    public class LogExportDto
    {
        public DateTime Timestamp { get; set; }
        public string AssetName { get; set; }
        public string AssetType { get; set; }
        public string EventType { get; set; }
        public string RawData { get; set; }
    }

    public class IncidentExportDto
    {
        public string Field { get; set; }
        public string Value { get; set; }
    }
}