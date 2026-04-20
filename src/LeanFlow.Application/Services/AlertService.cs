using LeanFlow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LeanFlow.Application.Services
{
    /// <summary>
    /// Alert Service — sends notifications via email and webhook
    /// Circle 2: Nervous System alerts
    /// </summary>
    public class AlertService
    {
        private readonly HttpClient _http;
        private readonly List<AlertMessage> _alertHistory = new();
        private readonly string? _webhookUrl;
        private readonly string? _emailTo;

        public AlertService(IHttpClientFactory factory)
        {
            _http = factory.CreateClient();
            _webhookUrl = Environment.GetEnvironmentVariable("ALERT_WEBHOOK_URL");
            _emailTo = Environment.GetEnvironmentVariable("ALERT_EMAIL");
        }

        public async Task SendAlertsAsync(List<AlertMessage> alerts)
        {
            foreach (var alert in alerts)
            {
                _alertHistory.Add(alert);
                Console.WriteLine($"[ALERT] {alert.Severity} — {alert.Title}: {alert.Body}");

                // Send to webhook if configured (Slack, Teams, Discord, etc.)
                if (!string.IsNullOrEmpty(_webhookUrl))
                    await SendWebhookAsync(alert);
            }
        }

        private async Task SendWebhookAsync(AlertMessage alert)
        {
            try
            {
                var emoji = alert.Severity == AlertSeverity.Critical ? "🔴" : alert.Severity == AlertSeverity.Warning ? "⚠️" : "ℹ️";
                var payload = new { text = $"{emoji} *{alert.Title}*\n{alert.Body}\n_{alert.Timestamp:yyyy-MM-dd HH:mm} UTC_" };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync(_webhookUrl, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Webhook send failed: {ex.Message}");
            }
        }

        public List<AlertMessage> GetAlertHistory() => _alertHistory;
        public List<AlertMessage> GetRecentAlerts(int count = 10) => _alertHistory.TakeLast(count).ToList();
    }
}
