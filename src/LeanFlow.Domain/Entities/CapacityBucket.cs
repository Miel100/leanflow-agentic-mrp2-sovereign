using System.Collections.Generic;

namespace LeanFlow.Domain.Entities
{
    public class CapacityBucket
    {
        public string MachineGroup { get; set; } = string.Empty;
        public int Week { get; set; }
        public decimal AvailableHours { get; set; }
        public decimal LoadedHours { get; set; }
        public decimal LoadPct => AvailableHours > 0 ? (LoadedHours / AvailableHours) * 100 : 0;
        public bool IsOverloaded => LoadPct > 100;
        public List<string> ItemsLoaded { get; set; } = new();
    }
}
