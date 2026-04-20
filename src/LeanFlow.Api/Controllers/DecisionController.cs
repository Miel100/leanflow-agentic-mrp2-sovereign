using LeanFlow.Application.Services;
using LeanFlow.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LeanFlow.Api.Controllers
{
    [ApiController]
    [Route("api/decisions")]
    public class DecisionController : ControllerBase
    {
        private readonly DecisionEngine _engine;

        public DecisionController(DecisionEngine engine)
        {
            _engine = engine;
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> Analyze()
        {
            var result = await _engine.AnalyzeAndDecideAsync();
            return Ok(result);
        }

        [HttpGet("pending")]
        public IActionResult GetPending()
        {
            return Ok(_engine.GetPendingApprovals());
        }

        [HttpGet("log")]
        public IActionResult GetLog()
        {
            return Ok(_engine.GetDecisionLog());
        }

        [HttpPost("approve/{id}")]
        public async Task<IActionResult> Approve(Guid id, [FromQuery] string approvedBy = "MANAGER")
        {
            var decision = await _engine.ApproveDecisionAsync(id, approvedBy);
            if (decision == null) return NotFound(new { error = "Decision not found" });
            return Ok(decision);
        }

        [HttpPost("reject/{id}")]
        public async Task<IActionResult> Reject(Guid id, [FromQuery] string rejectedBy = "MANAGER")
        {
            var decision = await _engine.RejectDecisionAsync(id, rejectedBy);
            if (decision == null) return NotFound(new { error = "Decision not found" });
            return Ok(decision);
        }

        [HttpPost("mode")]
        public IActionResult SetMode([FromQuery] string mode)
        {
            if (mode != ExecutionMode.Autonomous && mode != ExecutionMode.Approval)
                return BadRequest(new { error = "Mode must be AUTONOMOUS or APPROVAL" });
            _engine.SetMode(mode);
            return Ok(new { mode = mode, message = $"Decision engine set to {mode} mode" });
        }

        [HttpGet("mode")]
        public IActionResult GetMode()
        {
            return Ok(new { mode = _engine.GetMode() });
        }
    }
}
