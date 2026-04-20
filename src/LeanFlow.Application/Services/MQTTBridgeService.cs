using LeanFlow.Domain.Entities;
using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LeanFlow.Application.Services
{
    /// <summary>
    /// MQTT Bridge Service — Circle 4: Shopfloor Bridge
    /// Connects to MQTT broker and receives real machine data
    /// Topics: leanflow/machine/{machineId}/status
    ///         leanflow/inventory/{itemCode}/count
    ///         leanflow/production/{itemCode}/completed
    /// </summary>
    public class MQTTBridgeService
    {
        private readonly EventEngine _eventEngine;
        private readonly AlertService _alerts;
        private IMqttClient? _mqttClient;
        private bool _isConnected = false;
        private readonly List<MachineDataPoint> _dataLog = new();
        private readonly string _brokerHost;
        private readonly int _brokerPort;

        public MQTTBridgeService(EventEngine eventEngine, AlertService alerts)
        {
            _eventEngine = eventEngine;
            _alerts = alerts;
            _brokerHost = Environment.GetEnvironmentVariable("MQTT_BROKER_HOST") ?? "localhost";
            _brokerPort = int.Parse(Environment.GetEnvironmentVariable("MQTT_BROKER_PORT") ?? "1883");
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                var factory = new MqttFactory();
                _mqttClient = factory.CreateMqttClient();

                var username = Environment.GetEnvironmentVariable("MQTT_USERNAME") ?? "";
var password = Environment.GetEnvironmentVariable("MQTT_PASSWORD") ?? "";

var tlsOptions = new MqttClientTlsOptionsBuilder()
    .UseTls()
    .Build();

var options = new MqttClientOptionsBuilder()
    .WithTcpServer(_brokerHost, _brokerPort)
    .WithClientId($"leanflow-mrp2-{Guid.NewGuid()}")
    .WithCleanSession()
    .WithTlsOptions(tlsOptions)
    .WithCredentials(username, password)
    .Build();

                _mqttClient.ApplicationMessageReceivedAsync += HandleMessageAsync;

                await _mqttClient.ConnectAsync(options, CancellationToken.None);

                // Subscribe to all LeanFlow topics
                await _mqttClient.SubscribeAsync("leanflow/#");

                _isConnected = true;
                Console.WriteLine($"[MQTT] Connected to broker: {_brokerHost}:{_brokerPort}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Connection failed: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_mqttClient != null && _isConnected)
            {
                await _mqttClient.DisconnectAsync();
                _isConnected = false;
                Console.WriteLine("[MQTT] Disconnected from broker");
            }
        }

        private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            Console.WriteLine($"[MQTT] Message received: {topic} = {payload}");

            var dataPoint = new MachineDataPoint
            {
                Topic = topic,
                Payload = payload,
                ReceivedAt = DateTime.UtcNow
            };
            _dataLog.Add(dataPoint);

            try
            {
                // Route message based on topic
                if (topic.StartsWith("leanflow/production/"))
                {
                    await HandleProductionMessageAsync(topic, payload);
                }
                else if (topic.StartsWith("leanflow/inventory/"))
                {
                    await HandleInventoryMessageAsync(topic, payload);
                }
                else if (topic.StartsWith("leanflow/machine/"))
                {
                    await HandleMachineMessageAsync(topic, payload);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Message processing error: {ex.Message}");
            }
        }

        private async Task HandleProductionMessageAsync(string topic, string payload)
        {
            // Topic: leanflow/production/{itemCode}/completed
            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            var itemCode = parts[2];
            using var doc = JsonDocument.Parse(payload);
            var quantity = doc.RootElement.TryGetProperty("quantity", out var q) ? q.GetDecimal() : 0;
            var operatorId = doc.RootElement.TryGetProperty("operatorId", out var op) ? op.GetString() ?? "MQTT" : "MQTT";

            var evt = new OperatorEvent
            {
                EventType = OperatorEventTypes.WorkOrderCompleted,
                ItemCode = itemCode,
                Quantity = quantity,
                Notes = $"Auto-reported via MQTT from shopfloor",
                OperatorId = operatorId
            };

            await _eventEngine.ProcessEventAsync(evt);
            Console.WriteLine($"[MQTT] Production completed: {quantity} units of {itemCode}");
        }

        private async Task HandleInventoryMessageAsync(string topic, string payload)
        {
            // Topic: leanflow/inventory/{itemCode}/count
            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            var itemCode = parts[2];
            using var doc = JsonDocument.Parse(payload);
            var count = doc.RootElement.TryGetProperty("count", out var c) ? c.GetDecimal() : 0;

            var evt = new OperatorEvent
            {
                EventType = OperatorEventTypes.StockAdjustment,
                ItemCode = itemCode,
                Quantity = count,
                Notes = "Auto-reported via MQTT barcode/RFID scan",
                OperatorId = "MQTT-SCANNER"
            };

            await _eventEngine.ProcessEventAsync(evt);
            Console.WriteLine($"[MQTT] Inventory update: {itemCode} = {count} units");
        }

        private async Task HandleMachineMessageAsync(string topic, string payload)
        {
            // Topic: leanflow/machine/{machineId}/status
            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            var machineId = parts[2];
            using var doc = JsonDocument.Parse(payload);
            var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            var downtimeHours = doc.RootElement.TryGetProperty("downtimeHours", out var d) ? d.GetDecimal() : 0;

            if (status == "DOWNTIME" && downtimeHours > 0)
            {
                var evt = new OperatorEvent
                {
                    EventType = OperatorEventTypes.MachineDowntime,
                    ItemCode = "N/A",
                    MachineGroup = machineId,
                    Quantity = downtimeHours,
                    Notes = $"Auto-reported via MQTT. Status: {status}",
                    OperatorId = "MQTT-SENSOR"
                };

                await _eventEngine.ProcessEventAsync(evt);
                Console.WriteLine($"[MQTT] Machine downtime: {machineId} for {downtimeHours}h");
            }
        }

        // Simulate machine data for testing without real hardware
        public async Task SimulateShopfloorDataAsync()
        {
            var random = new Random();
            var simulations = new[]
            {
                new { topic = "leanflow/production/ITEM-001/completed", payload = "{\"quantity\": 50, \"operatorId\": \"SIM-001\"}" },
                new { topic = "leanflow/inventory/ITEM-003/count", payload = "{\"count\": 280}" },
                new { topic = "leanflow/machine/SMT-01/status", payload = "{\"status\": \"RUNNING\", \"downtimeHours\": 0}" },
                new { topic = "leanflow/production/ITEM-002/completed", payload = "{\"quantity\": 25, \"operatorId\": \"SIM-002\"}" },
            };

            foreach (var sim in simulations)
            {
                var fakeEvent = new MqttApplicationMessageReceivedEventArgs(
                    "simulator",
                    new MqttApplicationMessageBuilder()
                        .WithTopic(sim.topic)
                        .WithPayload(sim.payload)
                        .Build(),
                    new MQTTnet.Packets.MqttPublishPacket(),
                    null);

                await HandleMessageAsync(fakeEvent);
                await Task.Delay(100);
            }

            Console.WriteLine("[MQTT] Simulation completed");
        }

        public bool IsConnected => _isConnected;
        public string BrokerHost => _brokerHost;
        public int BrokerPort => _brokerPort;
        public List<MachineDataPoint> GetDataLog() => _dataLog;
    }

    public class MachineDataPoint
    {
        public string Topic { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
    }
}