using LeanFlow.Application.Agents;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LeanFlow.Api.Controllers
{
    [ApiController]
    [Route("api/mrp")]
    public class MRPController : ControllerBase
    {
        private readonly SupervisorAgent _supervisor;

        public MRPController(SupervisorAgent supervisor)
        {
            _supervisor = supervisor;
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
    }
}
