using System;
namespace LeanFlow.Domain.Entities
{
    public class DemandForecast
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ItemCode { get; set; } = string.Empty;
        public decimal ForecastedQuantity { get; set; }
        public DateTime ForecastDate { get; set; } = DateTime.UtcNow;
        public decimal ConfidenceLevel { get; set; } = 0.75m;
    }
}
