using System;
namespace LeanFlow.Domain.Entities
{
    public class WorkOrder
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string WorkOrderNumber { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Planned";
        public Guid RatingFileId { get; set; }
        public decimal ActualProcessingTime { get; set; }
    }
}
