using LeanFlow.Application.Agents;
using LeanFlow.Application.Services;
using LeanFlow.Application.Engine;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeanFlow.Api.Controllers
{
    [ApiController]
    [Route("api/mrp")]
    public class MRPController : ControllerBase
    {
        private readonly SupervisorAgent _supervisor;
        private readonly LeanMRP2Service _service;
        private readonly FactoryConfigAgent _configAgent;
        private readonly WatchdogService _watchdog;

        public MRPController(SupervisorAgent supervisor, LeanMRP2Service service, FactoryConfigAgent configAgent, WatchdogService watchdog)
        {
            _supervisor = supervisor;
            _service = service;
            _configAgent = configAgent;
            _watchdog = watchdog;
        }

        [HttpGet("run")]
        public async Task<IActionResult> Run()
        {
            var result = await _supervisor.RunFullMRPCycleAsync();
            return Ok(result);
        }

        [HttpGet("status")]
        public IActionResult Status()
        {
            return Ok(new { status = "healthy", agents = new[] { "Demand", "RCCP", "CRP", "SFC", "Supervisor", "FactoryConfig" } });
        }

        [HttpPost("prompt")]
        public async Task<IActionResult> Prompt([FromBody] string prompt)
        {
            var response = await _supervisor.ProcessPromptAsync(prompt);
            return Ok(new { prompt = prompt, response = response });
        }

        [HttpGet("calculate")]
        public IActionResult Calculate([FromQuery] int weeks = 4)
        {
            var result = _service.RunMRP2(weeks);
            return Ok(result);
        }

        [HttpGet("calculate/summary")]
        public IActionResult CalculateSummary([FromQuery] int weeks = 4)
        {
            var result = _service.RunMRP2(weeks);
            return Ok(new {
                runDate = result.RunDate,
                horizonWeeks = result.PlanningHorizonWeeks,
                totalWorkOrdersPlanned = result.TotalWorkOrdersPlanned,
                totalPlannedCost = result.TotalPlannedCost,
                totalExceptions = result.TotalExceptions,
                exceptions = result.Exceptions,
                purchaseOrderSuggestions = result.PurchaseOrderSuggestions,
                capacityOverloads = result.CapacityBuckets
                    .Where(b => b.IsOverloaded)
                    .Select(b => new { b.MachineGroup, b.Week, loadPct = b.LoadPct, b.LoadedHours, b.AvailableHours, b.ItemsLoaded })
            });
        }

        [HttpPost("configure")]
        public async Task<IActionResult> Configure([FromBody] string factoryDescription)
        {
            if (string.IsNullOrWhiteSpace(factoryDescription))
                return BadRequest(new { error = "Please provide a factory description" });
            var config = await _configAgent.ExtractConfigAsync(factoryDescription);
            if (config.HasError)
                return BadRequest(new { error = config.Error });
            _service.ApplyConfiguration(config.RatingFiles, config.InventoryRecords);
            var mrpResult = _service.RunMRP2(config.PlanningHorizonWeeks, config.DemandForecasts);
            var plan = GeneratePlan(config, mrpResult);
            return Ok(new {
                factorySummary = config.FactorySummary,
                itemsConfigured = config.RatingFiles.Count,
                planningHorizonWeeks = config.PlanningHorizonWeeks,
                mrpSummary = new {
                    totalWorkOrdersPlanned = mrpResult.TotalWorkOrdersPlanned,
                    totalPlannedCost = mrpResult.TotalPlannedCost,
                    totalExceptions = mrpResult.TotalExceptions,
                    purchaseOrderSuggestions = mrpResult.PurchaseOrderSuggestions,
                    exceptions = mrpResult.Exceptions
                },
                actionablePlan = plan,
                configuredItems = config.RatingFiles.Select(r => new {
                    r.ItemCode, r.Description, r.MachineGroup,
                    r.ShiftPerDay, utilizationPct = r.UtilizationTarget * 100,
                    r.BatchQuantity, r.CostPerUnit
                })
            });
        }

        [HttpGet("watchdog/run")]
        public async Task<IActionResult> RunWatchdog()
        {
            var result = await _watchdog.RunCheckAsync();
            return Ok(result);
        }

        [HttpGet("watchdog/status")]
        public IActionResult WatchdogStatus()
        {
            return Ok(new {
                lastRunTime = _watchdog.GetLastRunTime(),
                checkHistory = _watchdog.GetCheckHistory().TakeLast(10)
            });
        }

        private string GeneratePlan(FactoryConfigResult config, LeanFlow.Domain.Entities.MRPRunResult mrp)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"FACTORY PLAN — {System.DateTime.UtcNow:yyyy-MM-dd}");
            sb.AppendLine($"Factory: {config.FactorySummary}");
            sb.AppendLine($"Planning horizon: {config.PlanningHorizonWeeks} weeks");
            sb.AppendLine();
            sb.AppendLine($"WORK ORDERS: {mrp.TotalWorkOrdersPlanned} planned");
            sb.AppendLine($"TOTAL PLANNED COST: ");
            sb.AppendLine();
            if (mrp.PurchaseOrderSuggestions.Any())
            {
                sb.AppendLine("PURCHASE ORDERS REQUIRED:");
                foreach (var po in mrp.PurchaseOrderSuggestions)
                    sb.AppendLine($"  {po}");
                sb.AppendLine();
            }
            if (mrp.Exceptions.Any())
            {
                sb.AppendLine("ALERTS:");
                foreach (var ex in mrp.Exceptions)
                    sb.AppendLine($"  {ex}");
                sb.AppendLine();
            }
            else
                sb.AppendLine("✅ NO ALERTS — All capacity within limits");
            sb.AppendLine("System configured and ready. No data retained after this session.");
            return sb.ToString();
        }
    }
}

