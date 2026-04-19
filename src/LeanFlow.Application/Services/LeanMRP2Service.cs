using LeanFlow.Domain.Entities;
using LeanFlow.Application.Engine;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LeanFlow.Application.Services
{
    public class LeanMRP2Service
    {
        private readonly List<RatingFile> _ratings = new();
        private readonly List<InventoryRecord> _inventory = new();
        private readonly MRP2Engine _engine = new();

        public LeanMRP2Service()
        {
            SeedRatingFiles();
            SeedInventory();
        }

        private void SeedRatingFiles()
        {
            _ratings.Add(new RatingFile { ItemCode = "ITEM-001", Description = "Steel Frame Assembly", ProcessingTime = 2.5m, SetupTime = 0.5m, BatchQuantity = 100, UtilizationTarget = 0.85m, MachineGroup = "PRESS-01", ShiftPerDay = 3, CostPerUnit = 45.50m });
            _ratings.Add(new RatingFile { ItemCode = "ITEM-002", Description = "Hydraulic Pump Unit", ProcessingTime = 4.0m, SetupTime = 1.0m, BatchQuantity = 50, UtilizationTarget = 0.80m, MachineGroup = "LATHE-02", ShiftPerDay = 2, CostPerUnit = 120.00m });
            _ratings.Add(new RatingFile { ItemCode = "ITEM-003", Description = "Electronic Control Board", ProcessingTime = 1.5m, SetupTime = 0.25m, BatchQuantity = 200, UtilizationTarget = 0.90m, MachineGroup = "SMT-01", ShiftPerDay = 3, CostPerUnit = 85.00m });
            _ratings.Add(new RatingFile { ItemCode = "ITEM-004", Description = "Aluminium Housing", ProcessingTime = 3.0m, SetupTime = 0.75m, BatchQuantity = 75, UtilizationTarget = 0.82m, MachineGroup = "CNC-03", ShiftPerDay = 2, CostPerUnit = 67.25m });
            _ratings.Add(new RatingFile { ItemCode = "ITEM-005", Description = "Conveyor Belt Module", ProcessingTime = 5.5m, SetupTime = 1.5m, BatchQuantity = 30, UtilizationTarget = 0.78m, MachineGroup = "WELD-01", ShiftPerDay = 1, CostPerUnit = 210.00m });
        }

        private void SeedInventory()
        {
            _inventory.Add(new InventoryRecord { ItemCode = "ITEM-001", Description = "Steel Frame Assembly", CurrentStock = 450, SafetyStock = 100, ReorderPoint = 150 });
            _inventory.Add(new InventoryRecord { ItemCode = "ITEM-002", Description = "Hydraulic Pump Unit", CurrentStock = 80, SafetyStock = 20, ReorderPoint = 40 });
            _inventory.Add(new InventoryRecord { ItemCode = "ITEM-003", Description = "Electronic Control Board", CurrentStock = 320, SafetyStock = 50, ReorderPoint = 80 });
            _inventory.Add(new InventoryRecord { ItemCode = "ITEM-004", Description = "Aluminium Housing", CurrentStock = 120, SafetyStock = 30, ReorderPoint = 60 });
            _inventory.Add(new InventoryRecord { ItemCode = "ITEM-005", Description = "Conveyor Belt Module", CurrentStock = 15, SafetyStock = 5, ReorderPoint = 10 });
        }

        public async Task<List<WorkOrder>> RunSFCAsync(DemandForecast forecast)
        {
            var result = new List<WorkOrder>();
            foreach (var r in _ratings)
                result.Add(new WorkOrder { ItemCode = r.ItemCode, Quantity = r.BatchQuantity, StartDate = System.DateTime.UtcNow, DueDate = System.DateTime.UtcNow.AddDays(5), Status = "Planned", RatingFileId = r.Id });
            return result;
        }

        public MRPRunResult RunMRP2(int horizonWeeks = 4)
        {
            var forecasts = new List<DemandForecast>
            {
                new DemandForecast { ItemCode = "ITEM-001", ForecastedQuantity = 320 },
                new DemandForecast { ItemCode = "ITEM-002", ForecastedQuantity = 160 },
                new DemandForecast { ItemCode = "ITEM-003", ForecastedQuantity = 600 },
                new DemandForecast { ItemCode = "ITEM-004", ForecastedQuantity = 240 },
                new DemandForecast { ItemCode = "ITEM-005", ForecastedQuantity = 80 },
            };
            return _engine.Calculate(_ratings, _inventory, forecasts, horizonWeeks);
        }

        public List<RatingFile> GetRatingFiles() => _ratings;
        public List<InventoryRecord> GetInventory() => _inventory;
    }
}
