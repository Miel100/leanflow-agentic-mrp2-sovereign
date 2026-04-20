using LeanFlow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LeanFlow.Application.Services
{
    /// <summary>
    /// Event Engine — processes operator events and triggers replanning
    /// Core of Circle 2: Nervous System
    /// </summary>
    public class EventEngine
    {
        private readonly LeanMRP2Service _mrp;
        private readonly AlertService _alerts;
        private readonly List<OperatorEvent> _eventLog = new();

        public EventEngine(LeanMRP2Service mrp, AlertService alerts)
        {
            _mrp = mrp;
            _alerts = alerts;
        }

        public async Task<EventProcessResult> ProcessEventAsync(OperatorEvent evt)
        {
            _eventLog.Add(evt);
            var result = new EventProcessResult { Event = evt };

            switch (evt.EventType)
            {
                case OperatorEventTypes.WorkOrderCompleted:
                    await HandleWorkOrderCompletedAsync(evt, result);
                    break;
                case OperatorEventTypes.StockReceived:
                    await HandleStockReceivedAsync(evt, result);
                    break;
                case OperatorEventTypes.MachineDowntime:
                    await HandleMachineDowntimeAsync(evt, result);
                    break;
                case OperatorEventTypes.QualityReject:
                    await HandleQualityRejectAsync(evt, result);
                    break;
                case OperatorEventTypes.StockAdjustment:
                    await HandleStockAdjustmentAsync(evt, result);
                    break;
                default:
                    result.Message = $"Unknown event type: {evt.EventType}";
                    break;
            }

            return result;
        }

        private async Task HandleWorkOrderCompletedAsync(OperatorEvent evt, EventProcessResult result)
        {
            // Update inventory
            var inv = _mrp.GetInventory().FirstOrDefault(i => i.ItemCode == evt.ItemCode);
            if (inv != null)
            {
                inv.CurrentStock += evt.Quantity;
                inv.LastUpdated = DateTime.UtcNow;
            }

            // Rerun MRP
            var mrpResult = _mrp.RunMRP2();
            result.MRPRerun = true;
            result.Message = $"Work order completed: {evt.Quantity} units of {evt.ItemCode} added to stock. MRP recalculated.";

            // Check for alerts
            var alerts = new List<AlertMessage>();
            if (inv != null && inv.CurrentStock < inv.SafetyStock)
                alerts.Add(new AlertMessage { Type = "STOCK", Title = $"Low Stock: {evt.ItemCode}", Body = $"Stock ({inv.CurrentStock}) below safety stock ({inv.SafetyStock}). Purchase order required.", Severity = AlertSeverity.Warning });

            if (mrpResult.Exceptions.Any())
                alerts.Add(new AlertMessage { Type = "MRP", Title = "MRP Exceptions Detected", Body = string.Join("\n", mrpResult.Exceptions), Severity = AlertSeverity.Warning });

            result.Alerts = alerts;
            await _alerts.SendAlertsAsync(alerts);
        }

        private async Task HandleStockReceivedAsync(OperatorEvent evt, EventProcessResult result)
        {
            var inv = _mrp.GetInventory().FirstOrDefault(i => i.ItemCode == evt.ItemCode);
            if (inv != null)
            {
                inv.CurrentStock += evt.Quantity;
                inv.LastUpdated = DateTime.UtcNow;
            }
            var mrpResult = _mrp.RunMRP2();
            result.MRPRerun = true;
            result.Message = $"Stock received: {evt.Quantity} units of {evt.ItemCode}. New stock: {inv?.CurrentStock}. MRP recalculated.";
            result.Alerts = new List<AlertMessage>();
            await _alerts.SendAlertsAsync(result.Alerts);
        }

        private async Task HandleMachineDowntimeAsync(OperatorEvent evt, EventProcessResult result)
        {
            var rating = _mrp.GetRatingFiles().FirstOrDefault(r => r.MachineGroup == evt.MachineGroup);
            var alerts = new List<AlertMessage>();
            alerts.Add(new AlertMessage
            {
                Type = "MACHINE",
                Title = $"Machine Downtime: {evt.MachineGroup}",
                Body = $"Machine {evt.MachineGroup} reported downtime of {evt.Quantity} hours. Notes: {evt.Notes}. Capacity replanning required.",
                Severity = AlertSeverity.Critical
            });
            var mrpResult = _mrp.RunMRP2();
            result.MRPRerun = true;
            result.Message = $"Machine downtime reported for {evt.MachineGroup}. MRP recalculated with reduced capacity.";
            result.Alerts = alerts;
            await _alerts.SendAlertsAsync(alerts);
        }

        private async Task HandleQualityRejectAsync(OperatorEvent evt, EventProcessResult result)
        {
            var inv = _mrp.GetInventory().FirstOrDefault(i => i.ItemCode == evt.ItemCode);
            if (inv != null)
            {
                inv.CurrentStock = Math.Max(0, inv.CurrentStock - evt.Quantity);
                inv.LastUpdated = DateTime.UtcNow;
            }
            var alerts = new List<AlertMessage>();
            alerts.Add(new AlertMessage
            {
                Type = "QUALITY",
                Title = $"Quality Reject: {evt.ItemCode}",
                Body = $"{evt.Quantity} units of {evt.ItemCode} rejected. Stock adjusted. Notes: {evt.Notes}",
                Severity = AlertSeverity.Warning
            });
            var mrpResult = _mrp.RunMRP2();
            result.MRPRerun = true;
            result.Message = $"Quality reject processed: {evt.Quantity} units removed from stock. MRP recalculated.";
            result.Alerts = alerts;
            await _alerts.SendAlertsAsync(alerts);
        }

        private async Task HandleStockAdjustmentAsync(OperatorEvent evt, EventProcessResult result)
        {
            var inv = _mrp.GetInventory().FirstOrDefault(i => i.ItemCode == evt.ItemCode);
            if (inv != null)
            {
                inv.CurrentStock = evt.Quantity;
                inv.LastUpdated = DateTime.UtcNow;
            }
            var mrpResult = _mrp.RunMRP2();
            result.MRPRerun = true;
            result.Message = $"Stock adjusted: {evt.ItemCode} set to {evt.Quantity} units. MRP recalculated.";
            result.Alerts = new List<AlertMessage>();
            await _alerts.SendAlertsAsync(result.Alerts);
        }

        public List<OperatorEvent> GetEventLog() => _eventLog;
    }

    public class EventProcessResult
    {
        public OperatorEvent Event { get; set; } = new();
        public bool MRPRerun { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<AlertMessage> Alerts { get; set; } = new();
    }
}
