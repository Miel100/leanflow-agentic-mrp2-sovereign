using LeanFlow.Application.Services;
using LeanFlow.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LeanFlow.Api.Controllers
{
    [ApiController]
    [Route("api/operator")]
    public class OperatorController : ControllerBase
    {
        private readonly EventEngine _eventEngine;
        private readonly AlertService _alertService;

        public OperatorController(EventEngine eventEngine, AlertService alertService)
        {
            _eventEngine = eventEngine;
            _alertService = alertService;
        }

        /// POST api/operator/event
        /// Operator reports a shopfloor event
        [HttpPost("event")]
        public async Task<IActionResult> PostEvent([FromBody] OperatorEvent evt)
        {
            if (string.IsNullOrEmpty(evt.EventType) || string.IsNullOrEmpty(evt.ItemCode))
                return BadRequest(new { error = "EventType and ItemCode are required" });
            var result = await _eventEngine.ProcessEventAsync(evt);
            return Ok(new {
                success = true,
                message = result.Message,
                mrpRerun = result.MRPRerun,
                alertsGenerated = result.Alerts.Count,
                alerts = result.Alerts
            });
        }

        /// GET api/operator/events
        /// Get event log
        [HttpGet("events")]
        public IActionResult GetEvents()
        {
            return Ok(_eventEngine.GetEventLog());
        }

        /// GET api/operator/alerts
        /// Get recent alerts
        [HttpGet("alerts")]
        public IActionResult GetAlerts()
        {
            return Ok(_alertService.GetRecentAlerts(20));
        }
    }
}
