using System;
using System.Collections.Generic;

namespace LeanFlow.Domain.Entities
{
    public class AutonomousDecision
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DecisionType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string MachineGroup { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal EstimatedCost { get; set; }
        public string Justification { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExecutedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovedBy { get; set; } = string.Empty;
        public string Status { get; set; } = DecisionStatus.Pending;
        public string ExecutionMode { get; set; } = Entities.ExecutionMode.Approval;
        public string Result { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public static class DecisionType
    {
        public const string PurchaseOrder = "PURCHASE_ORDER";
        public const string WorkOrderReschedule = "WO_RESCHEDULE";
        public const string DemandRebalance = "DEMAND_REBALANCE";
        public const string CapacityAdjustment = "CAPACITY_ADJUSTMENT";
        public const string SafetyStockReplenish = "SAFETY_STOCK_REPLENISH";
    }

    public static class DecisionStatus
    {
        public const string Pending = "PENDING";
        public const string AwaitingApproval = "AWAITING_APPROVAL";
        public const string Approved = "APPROVED";
        public const string Rejected = "REJECTED";
        public const string Executed = "EXECUTED";
        public const string Failed = "FAILED";
    }

    public static class ExecutionMode
    {
        public const string Autonomous = "AUTONOMOUS";
        public const string Approval = "APPROVAL";
    }
}
