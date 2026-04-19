using LeanFlow.Application.Agents;
using LeanFlow.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LeanFlow.Api.Controllers
{
    [ApiController]
    [Route("api/mrp")]
    public class MRPController : ControllerBase
    {
        private readonly SupervisorAgent _supervisor;
        private readonly LeanMRP2Service _service;

        public MRPController(SupervisorAgent supervisor, LeanMRP2Service service)
        {
            _supervisor = supervisor;
            _service = service;
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
            return Ok(new { status = "healthy", agents = new[] { "Demand", "RCCP", "CRP", "SFC", "Supervisor" } });
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
    }
}
