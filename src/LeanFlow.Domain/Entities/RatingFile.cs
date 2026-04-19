using System;
using System.Collections.Generic;
namespace LeanFlow.Domain.Entities
{
    public class RatingFile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal ProcessingTime { get; set; }
        public decimal SetupTime { get; set; }
        public int BatchQuantity { get; set; }
        public decimal UtilizationTarget { get; set; } = 0.85m;
        public string MachineGroup { get; set; } = string.Empty;
        public int ShiftPerDay { get; set; } = 3;
        public decimal CostPerUnit { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
