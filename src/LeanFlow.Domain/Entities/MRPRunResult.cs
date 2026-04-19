using System;
using System.Collections.Generic;

namespace LeanFlow.Domain.Entities
{
    public class MRPRunResult
    {
        public DateTime RunDate { get; set; } = DateTime.UtcNow;
        public int PlanningHorizonWeeks { get; set; }
        public List<MRPCalculationRow> Rows { get; set; } = new();
        public List<CapacityBucket> CapacityBuckets { get; set; } = new();
        public List<string> Exceptions { get; set; } = new();
        public List<string> PurchaseOrderSuggestions { get; set; } = new();
        public decimal TotalPlannedCost { get; set; }
        public int TotalWorkOrdersPlanned { get; set; }
        public int TotalExceptions { get; set; }
    }
}
