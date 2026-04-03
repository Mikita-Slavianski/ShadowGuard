using Microsoft.EntityFrameworkCore;
using NewShadowGuard.Data;
using NewShadowGuard.Models;

namespace NewShadowGuard.Services
{
    public class CorrelationService
    {
        private readonly ApplicationDbContext _context;

        public CorrelationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CorrelationResult> RunCorrelation(int? runByUserId = null)
        {
            var startTime = DateTime.UtcNow;
            var result = new CorrelationResult();

            // Получаем активные правила
            var rules = await _context.CorrelationRules
                .Where(r => r.IsActive)
                .ToListAsync();

            result.RulesChecked = rules.Count;

            foreach (var rule in rules)
            {
                var incidents = await CheckRule(rule);
                result.IncidentsCreated += incidents;
            }

            // Обновляем время последнего запуска правил
            foreach (var rule in rules)
            {
                rule.LastRunAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();

            // Записываем результат запуска
            var correlationRun = new CorrelationRun
            {
                RunAt = DateTime.UtcNow,
                RulesChecked = result.RulesChecked,
                IncidentsCreated = result.IncidentsCreated,
                RunBy = runByUserId,
                DurationSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds
            };
            _context.CorrelationRuns.Add(correlationRun);
            await _context.SaveChangesAsync();

            result.DurationSeconds = correlationRun.DurationSeconds;
            return result;
        }

        private async Task<int> CheckRule(CorrelationRule rule)
        {
            var incidentsCreated = 0;
            var timeWindow = DateTime.UtcNow.AddMinutes(-rule.TimeWindowMinutes);

            // Получаем логи за временное окно
            var logsQuery = _context.Logs
                .Include(l => l.Asset)
                .Where(l => l.Timestamp >= timeWindow);

            if (!string.IsNullOrEmpty(rule.EventType))
            {
                logsQuery = logsQuery.Where(l => l.EventType == rule.EventType);
            }

            var logs = await logsQuery.ToListAsync();

            // Группируем по активу и ищем нарушения порога
            var groupedLogs = logs.GroupBy(l => l.AssetId);

            foreach (var group in groupedLogs)
            {
                if (group.Count() >= rule.Threshold)
                {
                    // Проверяем, нет ли уже инцидента для этой группы логов
                    var firstLogId = group.First().LogId;
                    var existingIncident = await _context.Incidents
                        .FirstOrDefaultAsync(i => i.LogId == firstLogId && i.Status != "Resolved");

                    if (existingIncident == null)
                    {
                        // Создаём новый инцидент
                        var incident = new Incident
                        {
                            Title = GenerateIncidentTitle(rule, group.First()),
                            Description = GenerateIncidentDescription(rule, group),
                            Severity = rule.Severity,
                            Status = "New",
                            TenantId = group.First().Asset?.TenantId,
                            LogId = firstLogId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Incidents.Add(incident);
                        incidentsCreated++;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return incidentsCreated;
        }

        private string GenerateIncidentTitle(CorrelationRule rule, Log log)
        {
            return $"{rule.RuleName} - {log.EventType} на {log.Asset?.Name ?? "Unknown"}";
        }

        private string GenerateIncidentDescription(CorrelationRule rule, IGrouping<int?, Log> logs)
        {
            var firstLog = logs.First();
            var lastLog = logs.Last();

            return $@"
Правило: {rule.RuleName}
Порог срабатывания: {rule.Threshold} событий за {rule.TimeWindowMinutes} мин.
Фактически событий: {logs.Count()}

Затронутый актив: {firstLog.Asset?.Name} ({firstLog.Asset?.IpAddress})
Первое событие: {firstLog.Timestamp:dd.MM.yyyy HH:mm:ss}
Последнее событие: {lastLog.Timestamp:dd.MM.yyyy HH:mm:ss}

Данные последнего события:
{firstLog.RawData}
            ".Trim();
        }
    }

    public class CorrelationResult
    {
        public int RulesChecked { get; set; }
        public int IncidentsCreated { get; set; }
        public int DurationSeconds { get; set; }
    }
}