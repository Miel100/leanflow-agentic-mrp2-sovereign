using System;
namespace LeanFlow.Domain.Entities
{
    public class InventoryRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal SafetyStock { get; set; }
        public decimal ReorderPoint { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
