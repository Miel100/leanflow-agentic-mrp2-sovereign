using System;

namespace LeanFlow.Domain.Entities
{
    public class MRPCalculationRow
    {
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MachineGroup { get; set; } = string.Empty;
        public int Week { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;

        // Core MRP calculations
        public decimal GrossDemand { get; set; }          // What is needed
        public decimal ScheduledReceipts { get; set; }    // Already ordered, arriving this period
        public decimal ProjectedOnHand { get; set; }      // Stock at end of period
        public decimal NetRequirement { get; set; }       // What still needs to be produced
        public decimal PlannedOrderReceipt { get; set; }  // Planned production arriving
        public decimal PlannedOrderRelease { get; set; }  // When to start production (offset by lead time)

        // Capacity
        public decimal CapacityRequired { get; set; }     // Hours needed on machine
        public decimal CapacityAvailable { get; set; }    // Hours available on machine
        public decimal CapacityLoadPct { get; set; }      // Load percentage

        // Alerts
        public bool IsCapacityOverloaded => CapacityLoadPct > 100;
        public bool IsStockoutRisk => ProjectedOnHand < 0;
        public bool IsBelowSafetyStock { get; set; }
        public bool RequiresPurchaseOrder { get; set; }

        // Cost
        public decimal PlannedOrderCost { get; set; }
    }
}
