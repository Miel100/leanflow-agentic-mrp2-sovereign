using LeanFlow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LeanFlow.Application.Engine
{
    /// <summary>
    /// Core MRP2 Calculation Engine
    /// Implements time-phased net requirements planning with capacity loading
    /// Based on the Lean MRP2 philosophy from the original document
    /// </summary>
    public class MRP2Engine
    {
        private const int HoursPerShift = 8;
        private const int DaysPerWeek = 5;
        private const int DefaultLeadTimeWeeks = 1;

        /// <summary>
        /// Run full MRP2 calculation over a planning horizon
        /// </summary>
        public MRPRunResult Calculate(
            List<RatingFile> ratingFiles,
            List<InventoryRecord> inventory,
            List<DemandForecast> forecasts,
            int horizonWeeks = 4)
        {
            var result = new MRPRunResult { PlanningHorizonWeeks = horizonWeeks };
            var periods = GeneratePlanningPeriods(horizonWeeks);
            var capacityBuckets = InitializeCapacityBuckets(ratingFiles, periods);

            foreach (var rating in ratingFiles)
            {
                var inv = inventory.FirstOrDefault(i => i.ItemCode == rating.ItemCode);
                decimal openingStock = inv?.CurrentStock ?? 0;
                decimal safetyStock = inv?.SafetyStock ?? 0;
                decimal reorderPoint = inv?.ReorderPoint ?? 0;

                // Calculate lead time in weeks (minimum 1)
                int leadTimeWeeks = Math.Max(1, (int)Math.Ceiling((rating.ProcessingTime + rating.SetupTime) / (HoursPerShift * rating.ShiftPerDay)));

                // EOQ calculation (Economic Order Quantity)
                decimal annualDemand = rating.BatchQuantity * 52m;
                decimal orderingCost = 50m; // Fixed ordering cost
                decimal holdingCostRate = 0.25m; // 25% of unit cost per year
                decimal eoq = (decimal)Math.Sqrt((double)(2 * annualDemand * orderingCost / (holdingCostRate * rating.CostPerUnit)));
                decimal lotSize = Math.Max(rating.BatchQuantity, Math.Round(eoq / rating.BatchQuantity) * rating.BatchQuantity);

                decimal projectedOnHand = openingStock;

                foreach (var period in periods)
                {
                    // Get demand for this period from forecasts or use average
                    var periodForecast = forecasts.FirstOrDefault(f => f.ItemCode == rating.ItemCode);
                    decimal grossDemand = periodForecast != null
                        ? periodForecast.ForecastedQuantity / horizonWeeks
                        : rating.BatchQuantity * 0.8m; // Default: 80% of batch qty per week

                    decimal scheduledReceipts = 0; // Would come from open POs in full system

                    // Core MRP net requirements calculation
                    decimal netRequirement = Math.Max(0, grossDemand - projectedOnHand - scheduledReceipts + safetyStock);

                    // Lot sizing — round up to nearest lot size
                    decimal plannedOrderReceipt = 0;
                    if (netRequirement > 0)
                        plannedOrderReceipt = Math.Ceiling(netRequirement / lotSize) * lotSize;

                    // Update projected on hand
                    projectedOnHand = projectedOnHand + scheduledReceipts + plannedOrderReceipt - grossDemand;

                    // Lead time offset — when must we release the order?
                    int releaseWeek = Math.Max(1, period.Week - leadTimeWeeks);
                    decimal plannedOrderRelease = releaseWeek <= period.Week ? plannedOrderReceipt : 0;

                    // Capacity calculation
                    decimal hoursRequired = plannedOrderReceipt > 0
                        ? (rating.ProcessingTime + rating.SetupTime) * (plannedOrderReceipt / rating.BatchQuantity)
                        : 0;
                    decimal hoursAvailable = rating.ShiftPerDay * HoursPerShift * DaysPerWeek * rating.UtilizationTarget;
                    decimal loadPct = hoursAvailable > 0 ? (hoursRequired / hoursAvailable) * 100 : 0;

                    // Update capacity bucket
                    var bucket = capacityBuckets.First(b => b.MachineGroup == rating.MachineGroup && b.Week == period.Week);
                    bucket.LoadedHours += hoursRequired;
                    if (plannedOrderReceipt > 0) bucket.ItemsLoaded.Add(rating.ItemCode);

                    // Build calculation row
                    var row = new MRPCalculationRow
                    {
                        ItemCode = rating.ItemCode,
                        Description = rating.Description,
                        MachineGroup = rating.MachineGroup,
                        Week = period.Week,
                        PeriodLabel = period.Label,
                        GrossDemand = Math.Round(grossDemand, 1),
                        ScheduledReceipts = scheduledReceipts,
                        ProjectedOnHand = Math.Round(projectedOnHand, 1),
                        NetRequirement = Math.Round(netRequirement, 1),
                        PlannedOrderReceipt = plannedOrderReceipt,
                        PlannedOrderRelease = plannedOrderRelease,
                        CapacityRequired = Math.Round(hoursRequired, 2),
                        CapacityAvailable = Math.Round(hoursAvailable, 2),
                        CapacityLoadPct = Math.Round(loadPct, 1),
                        IsBelowSafetyStock = projectedOnHand < safetyStock,
                        RequiresPurchaseOrder = projectedOnHand < reorderPoint,
                        PlannedOrderCost = plannedOrderReceipt * rating.CostPerUnit
                    };

                    result.Rows.Add(row);

                    // Generate exceptions
                    if (row.IsCapacityOverloaded)
                        result.Exceptions.Add($"⚠️ CAPACITY OVERLOAD: {rating.MachineGroup} in {period.Label} — {loadPct:F0}% loaded ({hoursRequired:F1}h required / {hoursAvailable:F1}h available)");

                    if (row.IsStockoutRisk)
                        result.Exceptions.Add($"🚨 STOCKOUT RISK: {rating.ItemCode} in {period.Label} — Projected stock: {projectedOnHand:F0} units");

                    if (row.IsBelowSafetyStock && projectedOnHand >= 0)
                        result.Exceptions.Add($"⚠️ BELOW SAFETY STOCK: {rating.ItemCode} in {period.Label} — {projectedOnHand:F0} units (safety stock: {safetyStock})");

                    if (row.RequiresPurchaseOrder)
                        result.PurchaseOrderSuggestions.Add($"📦 PURCHASE ORDER: {rating.ItemCode} — {rating.Description} | Qty: {lotSize} | Release by: {period.StartDate:yyyy-MM-dd}");
                }
            }

            // Add capacity bucket results
            result.CapacityBuckets = capacityBuckets;

            // Add overloaded capacity exceptions
            foreach (var bucket in capacityBuckets.Where(b => b.IsOverloaded))
                result.Exceptions.Add($"🔴 MACHINE OVERLOAD: {bucket.MachineGroup} Week {bucket.Week} — {bucket.LoadPct:F0}% ({bucket.LoadedHours:F1}h / {bucket.AvailableHours:F1}h) | Items: {string.Join(", ", bucket.ItemsLoaded)}");

            // Totals
            result.TotalPlannedCost = result.Rows.Sum(r => r.PlannedOrderCost);
            result.TotalWorkOrdersPlanned = result.Rows.Count(r => r.PlannedOrderReceipt > 0);
            result.TotalExceptions = result.Exceptions.Count;

            return result;
        }

        private List<PlanningPeriod> GeneratePlanningPeriods(int weeks)
        {
            var periods = new List<PlanningPeriod>();
            var start = DateTime.UtcNow.Date;
            // Start from next Monday
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)start.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;
            start = start.AddDays(daysUntilMonday);

            for (int w = 1; w <= weeks; w++)
            {
                periods.Add(new PlanningPeriod
                {
                    Week = w,
                    StartDate = start.AddDays((w - 1) * 7),
                    EndDate = start.AddDays((w - 1) * 7 + 4)
                });
            }
            return periods;
        }

        private List<CapacityBucket> InitializeCapacityBuckets(List<RatingFile> ratings, List<PlanningPeriod> periods)
        {
            var buckets = new List<CapacityBucket>();
            var machineGroups = ratings.Select(r => r.MachineGroup).Distinct();
            foreach (var mg in machineGroups)
            {
                var rating = ratings.First(r => r.MachineGroup == mg);
                decimal availableHours = rating.ShiftPerDay * HoursPerShift * DaysPerWeek * rating.UtilizationTarget;
                foreach (var period in periods)
                {
                    buckets.Add(new CapacityBucket
                    {
                        MachineGroup = mg,
                        Week = period.Week,
                        AvailableHours = Math.Round(availableHours, 2)
                    });
                }
            }
            return buckets;
        }
    }
}
