using System;

namespace LeanFlow.Domain.Entities
{
    public class PlanningPeriod
    {
        public int Week { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Label => $"W{Week} ({StartDate:MMM dd})";
    }
}
