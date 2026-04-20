using LeanFlow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LeanFlow.Application.Services
{
    /// <summary>
    /// Decision Engine — Circle 5A: Autonomous Decision Making
    /// Two modes: Autonomous (acts immediately) and Approval (waits for human)
    /// </summary>
    public class DecisionEngine
    {
        private readonly LeanMRP2Service _mrp;
        private readonly AlertService _alerts;
        private readonly List<AutonomousDecision> _decisionLog = new();
        private string _executionMode = ExecutionMode.Approval; // Default: safe

        public DecisionEngine(LeanMRP2Service mrp, AlertService alerts)
        {
            _mrp = mrp;
            _alerts = alerts;
        }

        // Switch between autonomous and approval mode
        public void SetMode(string mode)
        {
            _executionMode = mode;
            Console.WriteLine($"[DECISION ENGINE] Mode set to: {mode}");
        }

        public string GetMode() => _executionMode;

        /// <summary>
        /// Main entry point — analyzes current factory state and makes decisions
        /// </summary>
        public async Task<DecisionEngineResult> AnalyzeAndDecideAsync()
        {
            var result = new DecisionEngineResult
            {
                Mode = _executionMode,
                AnalysisTime = DateTime.UtcNow
            };

            var mrpResult = _mrp.RunMRP2();
            var inventory = _mrp.GetInventory();
            var ratings = _mrp.GetRatingFiles();

            // Decision 1: Auto Purchase Orders
            var poDecisions = await EvaluatePurchaseOrdersAsync(inventory, ratings, mrpResult);
            result.Decisions.AddRange(poDecisions);

            // Decision 2: Work Order Reschedule
            var rescheduleDecisions = await EvaluateReschedulingAsync(ratings, mrpResult);
            result.Decisions.AddRange(rescheduleDecisions);

            // Decision 3: Demand Rebalance
            var rebalanceDecisions = await EvaluateDemandRebalanceAsync(inventory, ratings);
            result.Decisions.AddRange(rebalanceDecisions);

            // Execute or queue based on mode
            foreach (var decision in result.Decisions)
            {
                if (_executionMode == ExecutionMode.Autonomous)
                {
                    await ExecuteDecisionAsync(decision);
                }
                else
                {
                    decision.Status = DecisionStatus.AwaitingApproval;
                    await _alerts.SendAlertsAsync(new List<AlertMessage>
                    {
                        new AlertMessage
                        {
                            Type = "DECISION_PENDING",
                            Title = $"⏳ Approval Required: {decision.Title}",
                            Body = $"{decision.Description}\nEstimated cost: ${decision.EstimatedCost:N0}\nJustification: {decision.Justification}\nDecision ID: {decision.Id}",
                            Severity = AlertSeverity.Warning
                        }
                    });
                }
                _decisionLog.Add(decision);
            }

            result.TotalDecisions = result.Decisions.Count;
            result.ExecutedDecisions = result.Decisions.Count(d => d.Status == DecisionStatus.Executed);
            result.PendingApproval = result.Decisions.Count(d => d.Status == DecisionStatus.AwaitingApproval);

            return result;
        }

        private async Task<List<AutonomousDecision>> EvaluatePurchaseOrdersAsync(
            List<InventoryRecord> inventory,
            List<RatingFile> ratings,
            MRPRunResult mrpResult)
        {
            var decisions = new List<AutonomousDecision>();

            foreach (var inv in inventory)
            {
                if (inv.CurrentStock <= inv.ReorderPoint)
                {
                    var rating = ratings.FirstOrDefault(r => r.ItemCode == inv.ItemCode);
                    decimal orderQty = rating?.BatchQuantity ?? 100;
                    decimal unitCost = rating?.CostPerUnit ?? 50;
                    decimal totalCost = orderQty * unitCost;
                    DateTime releaseDate = DateTime.UtcNow.AddDays(1);
                    DateTime deliveryDate = DateTime.UtcNow.AddDays(7);

                    decisions.Add(new AutonomousDecision
                    {
                        DecisionType = DecisionType.PurchaseOrder,
                        Title = $"Purchase Order: {inv.ItemCode}",
                        Description = $"Release PO for {orderQty} units of {inv.Description}",
                        ItemCode = inv.ItemCode,
                        Quantity = orderQty,
                        EstimatedCost = totalCost,
                        Justification = $"Stock ({inv.CurrentStock}) at or below reorder point ({inv.ReorderPoint}). " +
                                       $"Safety stock: {inv.SafetyStock}. " +
                                       $"Order {orderQty} units at ${unitCost}/unit = ${totalCost:N0}. " +
                                       $"Expected delivery: {deliveryDate:yyyy-MM-dd}.",
                        ExecutionMode = _executionMode,
                        Metadata = new Dictionary<string, string>
                        {
                            { "supplier", "DEFAULT_SUPPLIER" },
                            { "releaseDate", releaseDate.ToString("yyyy-MM-dd") },
                            { "deliveryDate", deliveryDate.ToString("yyyy-MM-dd") },
                            { "unitCost", unitCost.ToString() },
                            { "poNumber", $"PO-{DateTime.UtcNow:yyyyMMdd}-{inv.ItemCode}" }
                        }
                    });
                }
            }
            return decisions;
        }

        private async Task<List<AutonomousDecision>> EvaluateReschedulingAsync(
            List<RatingFile> ratings,
            MRPRunResult mrpResult)
        {
            var decisions = new List<AutonomousDecision>();

            foreach (var bucket in mrpResult.CapacityBuckets.Where(b => b.IsOverloaded))
            {
                var alternativeMachine = ratings
                    .Where(r => r.MachineGroup != bucket.MachineGroup && r.UtilizationTarget < 0.85m)
                    .FirstOrDefault();

                decisions.Add(new AutonomousDecision
                {
                    DecisionType = DecisionType.WorkOrderReschedule,
                    Title = $"Reschedule Work Orders: {bucket.MachineGroup}",
                    Description = $"Move work orders from overloaded {bucket.MachineGroup} to {alternativeMachine?.MachineGroup ?? "next available slot"}",
                    MachineGroup = bucket.MachineGroup,
                    Quantity = bucket.LoadedHours - bucket.AvailableHours,
                    EstimatedCost = 0,
                    Justification = $"{bucket.MachineGroup} at {bucket.LoadPct:F0}% capacity in Week {bucket.Week}. " +
                                   $"Overload: {bucket.LoadedHours - bucket.AvailableHours:F1}h. " +
                                   $"Recommend moving {string.Join(", ", bucket.ItemsLoaded)} to {alternativeMachine?.MachineGroup ?? "alternative slot"}.",
                    ExecutionMode = _executionMode,
                    Metadata = new Dictionary<string, string>
                    {
                        { "overloadHours", (bucket.LoadedHours - bucket.AvailableHours).ToString() },
                        { "alternativeMachine", alternativeMachine?.MachineGroup ?? "NONE" },
                        { "affectedItems", string.Join(",", bucket.ItemsLoaded) }
                    }
                });
            }
            return decisions;
        }

        private async Task<List<AutonomousDecision>> EvaluateDemandRebalanceAsync(
            List<InventoryRecord> inventory,
            List<RatingFile> ratings)
        {
            var decisions = new List<AutonomousDecision>();

            foreach (var inv in inventory.Where(i => i.CurrentStock < i.SafetyStock))
            {
                var rating = ratings.FirstOrDefault(r => r.ItemCode == inv.ItemCode);
                decimal deficit = inv.SafetyStock - inv.CurrentStock;
                decimal rebalanceQty = Math.Ceiling(deficit / (rating?.BatchQuantity ?? 100)) * (rating?.BatchQuantity ?? 100);
                decimal cost = rebalanceQty * (rating?.CostPerUnit ?? 50);

                decisions.Add(new AutonomousDecision
                {
                    DecisionType = DecisionType.DemandRebalance,
                    Title = $"Demand Rebalance: {inv.ItemCode}",
                    Description = $"Increase planned production by {rebalanceQty} units to restore safety stock",
                    ItemCode = inv.ItemCode,
                    Quantity = rebalanceQty,
                    EstimatedCost = cost,
                    Justification = $"Stock ({inv.CurrentStock}) below safety stock ({inv.SafetyStock}). " +
                                   $"Deficit: {deficit} units. " +
                                   $"Planned rebalance: {rebalanceQty} units at ${rating?.CostPerUnit ?? 50}/unit = ${cost:N0}.",
                    ExecutionMode = _executionMode,
                    Metadata = new Dictionary<string, string>
                    {
                        { "deficit", deficit.ToString() },
                        { "rebalanceQty", rebalanceQty.ToString() }
                    }
                });
            }
            return decisions;
        }

        public async Task<AutonomousDecision?> ApproveDecisionAsync(Guid decisionId, string approvedBy)
        {
            var decision = _decisionLog.FirstOrDefault(d => d.Id == decisionId);
            if (decision == null) return null;
            if (decision.Status != DecisionStatus.AwaitingApproval) return decision;

            decision.ApprovedBy = approvedBy;
            decision.ApprovedAt = DateTime.UtcNow;
            decision.Status = DecisionStatus.Approved;
            await ExecuteDecisionAsync(decision);
            return decision;
        }

        public async Task<AutonomousDecision?> RejectDecisionAsync(Guid decisionId, string rejectedBy)
        {
            var decision = _decisionLog.FirstOrDefault(d => d.Id == decisionId);
            if (decision == null) return null;
            decision.Status = DecisionStatus.Rejected;
            decision.Result = $"Rejected by {rejectedBy} at {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
            return decision;
        }

        private async Task ExecuteDecisionAsync(AutonomousDecision decision)
        {
            try
            {
                switch (decision.DecisionType)
                {
                    case DecisionType.PurchaseOrder:
                        // In a real system: call supplier API, create PO in ERP
                        decision.Result = $"PO {decision.Metadata.GetValueOrDefault("poNumber")} created. " +
                                         $"Qty: {decision.Quantity} units. " +
                                         $"Expected delivery: {decision.Metadata.GetValueOrDefault("deliveryDate")}. " +
                                         $"Total: ${decision.EstimatedCost:N0}";
                        break;

                    case DecisionType.WorkOrderReschedule:
                        decision.Result = $"Work orders rescheduled from {decision.MachineGroup} " +
                                         $"to {decision.Metadata.GetValueOrDefault("alternativeMachine")}. " +
                                         $"Overload hours resolved: {decision.Metadata.GetValueOrDefault("overloadHours")}h";
                        break;

                    case DecisionType.DemandRebalance:
                        decision.Result = $"Production plan increased by {decision.Quantity} units for {decision.ItemCode}. " +
                                         $"Safety stock will be restored. Cost: ${decision.EstimatedCost:N0}";
                        break;
                }

                decision.Status = DecisionStatus.Executed;
                decision.ExecutedAt = DateTime.UtcNow;

                await _alerts.SendAlertsAsync(new List<AlertMessage>
                {
                    new AlertMessage
                    {
                        Type = "DECISION_EXECUTED",
                        Title = $"✅ Decision Executed: {decision.Title}",
                        Body = decision.Result,
                        Severity = AlertSeverity.Info
                    }
                });

                Console.WriteLine($"[DECISION ENGINE] Executed: {decision.Title} — {decision.Result}");
            }
            catch (Exception ex)
            {
                decision.Status = DecisionStatus.Failed;
                decision.Result = $"Execution failed: {ex.Message}";
                Console.WriteLine($"[DECISION ENGINE] Failed: {decision.Title} — {ex.Message}");
            }
        }

        public List<AutonomousDecision> GetDecisionLog() => _decisionLog;
        public List<AutonomousDecision> GetPendingApprovals() =>
            _decisionLog.Where(d => d.Status == DecisionStatus.AwaitingApproval).ToList();
    }

    public class DecisionEngineResult
    {
        public string Mode { get; set; } = string.Empty;
        public DateTime AnalysisTime { get; set; }
        public List<AutonomousDecision> Decisions { get; set; } = new();
        public int TotalDecisions { get; set; }
        public int ExecutedDecisions { get; set; }
        public int PendingApproval { get; set; }
    }
}