using LeanFlow.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LeanFlow.Api.Controllers
{
    [ApiController]
    [Route("api/shopfloor")]
    public class ShopfloorController : ControllerBase
    {
        private readonly MQTTBridgeService _mqtt;

        public ShopfloorController(MQTTBridgeService mqtt)
        {
            _mqtt = mqtt;
        }

        [HttpPost("connect")]
        public async Task<IActionResult> Connect()
        {
            var connected = await _mqtt.ConnectAsync();
            return Ok(new {
                connected = connected,
                broker = _mqtt.BrokerHost,
                port = _mqtt.BrokerPort,
                message = connected ? "Connected to MQTT broker successfully" : "Failed to connect - check MQTT_BROKER_HOST environment variable"
            });
        }

        [HttpPost("disconnect")]
        public async Task<IActionResult> Disconnect()
        {
            await _mqtt.DisconnectAsync();
            return Ok(new { message = "Disconnected from MQTT broker" });
        }

        [HttpGet("status")]
        public IActionResult Status()
        {
            return Ok(new {
                connected = _mqtt.IsConnected,
                broker = _mqtt.BrokerHost,
                port = _mqtt.BrokerPort,
                messagesReceived = _mqtt.GetDataLog().Count,
                recentMessages = _mqtt.GetDataLog().TakeLast(10)
            });
        }

        [HttpPost("simulate")]
        public async Task<IActionResult> Simulate()
        {
            await _mqtt.SimulateShopfloorDataAsync();
            return Ok(new {
                message = "Shopfloor simulation completed",
                dataPoints = _mqtt.GetDataLog().Count,
                recentData = _mqtt.GetDataLog().TakeLast(10)
            });
        }
    }
}
