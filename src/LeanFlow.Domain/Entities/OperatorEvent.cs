using System;

namespace LeanFlow.Domain.Entities
{
    public class OperatorEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string MachineGroup { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string OperatorId { get; set; } = string.Empty;
    }

    public static class OperatorEventTypes
    {
        public const string WorkOrderCompleted = "WORK_ORDER_COMPLETED";
        public const string StockReceived = "STOCK_RECEIVED";
        public const string MachineDowntime = "MACHINE_DOWNTIME";
        public const string QualityReject = "QUALITY_REJECT";
        public const string StockAdjustment = "STOCK_ADJUSTMENT";
    }
}
