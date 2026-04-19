using LeanFlow.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LeanFlow.Application.Agents
{
    public class DemandAgent
    {
        public async Task<string> AnalyzeAsync(string itemCode, int forecastedQty, decimal currentStock, decimal reorderPoint)
        {
            string alert = currentStock <= reorderPoint ? " ⚠️ BELOW REORDER POINT" : "";
            return $"Demand forecast: {forecastedQty} units | Current stock: {currentStock}{alert}";
        }
    }
}
