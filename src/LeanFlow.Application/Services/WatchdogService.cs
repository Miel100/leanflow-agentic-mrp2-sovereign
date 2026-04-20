using LeanFlow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LeanFlow.Application.Services
{
    /// <summary>
    /// Watchdog Service — Circle 3: Continuous Factory Monitoring
    /// Runs automatically, detects deviations, fires proactive alerts
    /// </summary>
    public class WatchdogService
    {
        private readonly LeanMRP2Service _mrp;
        private readonly AlertService _alerts;
        private MRPRunResult? _lastMRPResult;
        private DateTime _lastRunTime = DateTime.MinValue;
        private readonly List<WatchdogCheckResult> _checkHistory = new();
private DateTime _lastAllowedRun = DateTime.MinValue;
private const int FreeTierCooldownHours = 24; // Free tier: once per day only

        // Thresholds
        private const decimal CapacityWarningThreshold = 85m;
        private const decimal CapacityCriticalThreshold = 100m;
        private const decimal StockWarningMultiplier = 1.2m;

        public WatchdogService(LeanMRP2Service mrp, AlertService alerts)
        {
            _mrp = mrp;
            _alerts = alerts;
        }

        public async Task<WatchdogCheckResult> RunCheckAsync()
        {// Free tier limitation — once per day only
var hoursSinceLastRun = (DateTime.UtcNow - _lastAllowedRun).TotalHours;
if (hoursSinceLastRun < FreeTierCooldownHours && _lastAllowedRun != DateTime.MinValue)
{
    var nextRun = _lastAllowedRun.AddHours(FreeTierCooldownHours);
    return new WatchdogCheckResult
    {
        Status = "FREE_TIER_LIMIT",
        Error = $"Free tier: Watchdog runs once per 24 hours. Next run available at {nextRun:yyyy-MM-dd HH:mm} UTC. Upgrade to Enterprise for every-15-minute monitoring.",
        CheckTime = DateTime.UtcNow
    };
}
_lastAllowedRun = DateTime.UtcNow;
            var checkResult = new WatchdogCheckResult
            {
                CheckTime = DateTime.UtcNow,
                Alerts = new List<AlertMessage>()
            };

            try
            {
                // Run MRP
                var mrpResult = _mrp.RunMRP2();
                var inventory = _mrp.GetInventory();
                var ratings = _mrp.GetRatingFiles();

                // Check 1: Stockout risks
                foreach (var inv in inventory)
                {
                    if (inv.CurrentStock <= 0)
                    {
                        checkResult.Alerts.Add(new AlertMessage
                        {
                            Type = "STOCKOUT",
                            Title = $"🚨 STOCKOUT: {inv.ItemCode}",
                            Body = $"{inv.Description} has ZERO stock. Immediate action required.",
                            Severity = AlertSeverity.Critical
                        });
                    }
                    else if (inv.CurrentStock < inv.SafetyStock)
                    {
                        checkResult.Alerts.Add(new AlertMessage
                        {
                            Type = "LOW_STOCK",
                            Title = $"⚠️ Low Stock: {inv.ItemCode}",
                            Body = $"{inv.Description} stock ({inv.CurrentStock}) below safety stock ({inv.SafetyStock}). Reorder point: {inv.ReorderPoint}.",
                            Severity = AlertSeverity.Warning
                        });
                    }
                    else if (inv.CurrentStock < inv.ReorderPoint * StockWarningMultiplier)
                    {
                        checkResult.Alerts.Add(new AlertMessage
                        {
                            Type = "REORDER",
                            Title = $"ℹ️ Reorder Soon: {inv.ItemCode}",
                            Body = $"{inv.Description} approaching reorder point. Current: {inv.CurrentStock}, Reorder at: {inv.ReorderPoint}.",
                            Severity = AlertSeverity.Info
                        });
                    }
                }

                // Check 2: Capacity overloads
                foreach (var bucket in mrpResult.CapacityBuckets.Where(b => b.LoadPct >= CapacityWarningThreshold))
                {
                    var severity = bucket.LoadPct >= CapacityCriticalThreshold
                        ? AlertSeverity.Critical : AlertSeverity.Warning;
                    checkResult.Alerts.Add(new AlertMessage
                    {
                        Type = "CAPACITY",
                        Title = $"{(bucket.LoadPct >= 100 ? "🔴" : "⚠️")} Capacity {(bucket.LoadPct >= 100 ? "OVERLOAD" : "WARNING")}: {bucket.MachineGroup}",
                        Body = $"Week {bucket.Week}: {bucket.MachineGroup} at {bucket.LoadPct:F0}% load ({bucket.LoadedHours:F1}h / {bucket.AvailableHours:F1}h available). Items: {string.Join(", ", bucket.ItemsLoaded)}",
                        Severity = severity
                    });
                }

                // Check 3: Purchase orders overdue
                foreach (var po in mrpResult.PurchaseOrderSuggestions)
                {
                    if (po.Contains("Release by:"))
                    {
                        var dateStr = po.Split("Release by:")[1].Trim();
                        if (DateTime.TryParse(dateStr, out var releaseDate))
                        {
                            if (releaseDate <= DateTime.UtcNow.AddDays(2))
                            {
                                checkResult.Alerts.Add(new AlertMessage
                                {
                                    Type = "PO_URGENT",
                                    Title = "🚨 URGENT Purchase Order",
                                    Body = po + " — Release date is within 48 hours!",
                                    Severity = AlertSeverity.Critical
                                });
                            }
                        }
                    }
                }

                // Check 4: Compare with last MRP run (deviation detection)
                if (_lastMRPResult != null)
                {
                    var costDeviation = Math.Abs(mrpResult.TotalPlannedCost - _lastMRPResult.TotalPlannedCost);
                    var costDeviationPct = _lastMRPResult.TotalPlannedCost > 0
                        ? (costDeviation / _lastMRPResult.TotalPlannedCost) * 100 : 0;

                    if (costDeviationPct > 20)
                    {
                        checkResult.Alerts.Add(new AlertMessage
                        {
                            Type = "PLAN_DEVIATION",
                            Title = "📊 Significant Plan Deviation",
                            Body = $"Planned cost changed by {costDeviationPct:F0}% since last check. Previous: ${_lastMRPResult.TotalPlannedCost:N0}, Current: ${mrpResult.TotalPlannedCost:N0}",
                            Severity = AlertSeverity.Warning
                        });
                    }

                    var woDeviation = Math.Abs(mrpResult.TotalWorkOrdersPlanned - _lastMRPResult.TotalWorkOrdersPlanned);
                    if (woDeviation > 2)
                    {
                        checkResult.Alerts.Add(new AlertMessage
                        {
                            Type = "WO_DEVIATION",
                            Title = "📋 Work Order Plan Changed",
                            Body = $"Work orders changed from {_lastMRPResult.TotalWorkOrdersPlanned} to {mrpResult.TotalWorkOrdersPlanned} since last check.",
                            Severity = AlertSeverity.Info
                        });
                    }
                }

                _lastMRPResult = mrpResult;
                _lastRunTime = DateTime.UtcNow;
                checkResult.TotalChecks = 4;
                checkResult.AlertsGenerated = checkResult.Alerts.Count;
                checkResult.Status = checkResult.Alerts.Any(a => a.Severity == AlertSeverity.Critical)
                    ? "CRITICAL" : checkResult.Alerts.Any(a => a.Severity == AlertSeverity.Warning)
                    ? "WARNING" : "HEALTHY";

                // Send alerts
                await _alerts.SendAlertsAsync(checkResult.Alerts);
                _checkHistory.Add(checkResult);

                return checkResult;
            }
            catch (Exception ex)
            {
                checkResult.Status = "ERROR";
                checkResult.Error = ex.Message;
                return checkResult;
            }
        }

        public DateTime GetLastRunTime() => _lastRunTime;
        public List<WatchdogCheckResult> GetCheckHistory() => _checkHistory;
    }

    public class WatchdogCheckResult
    {
        public DateTime CheckTime { get; set; }
        public string Status { get; set; } = "HEALTHY";
        public int TotalChecks { get; set; }
        public int AlertsGenerated { get; set; }
        public List<AlertMessage> Alerts { get; set; } = new();
        public string? Error { get; set; }
    }
}