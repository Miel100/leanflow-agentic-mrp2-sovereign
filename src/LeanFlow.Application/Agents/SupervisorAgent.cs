using LeanFlow.Domain.Entities;
using LeanFlow.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LeanFlow.Application.Agents
{
    public class SupervisorAgent
    {
        private readonly LeanMRP2Service _mrp;
        private readonly HttpClient _http;
        private readonly DemandAgent _demand;
        private readonly RCCPAgent _rccp;
        private readonly CRPAgent _crp;
        private readonly SFCAgent _sfc;
        private readonly string _groqApiKey;
        private const string GroqUrl = "https://api.groq.com/openai/v1/chat/completions";

        public SupervisorAgent(LeanMRP2Service mrp, IHttpClientFactory factory, DemandAgent demand, RCCPAgent rccp, CRPAgent crp, SFCAgent sfc)
        {
            _mrp = mrp;
            _demand = demand;
            _rccp = rccp;
            _crp = crp;
            _sfc = sfc;
            _groqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? string.Empty;
            _http = factory.CreateClient();
            _http.DefaultRequestHeaders.Add("Authorization", "Bearer " + _groqApiKey);
        }

        public async Task<string> ProcessPromptAsync(string userPrompt)
        {
            var systemPrompt = "You are the Supervisor Agent of a Lean MRP2 manufacturing system. You coordinate Demand, RCCP, CRP and SFC agents. Help planners customize Rating File, SFC logic, RCCP, CRP, and Demand Management. Be concise and practical.";
            var payload = new { model = "llama-3.1-8b-instant", messages = new[] { new { role = "system", content = systemPrompt }, new { role = "user", content = userPrompt } }, max_tokens = 512 };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(GroqUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return "Groq API error: " + responseJson;
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "No response";
        }

        public async Task<object> RunFullMRPCycleAsync()
        {
            var forecast = new DemandForecast { ItemCode = "ALL", ForecastedQuantity = 500 };
            var workOrders = await _mrp.RunSFCAsync(forecast);
            var ratings = _mrp.GetRatingFiles();
            var inventory = _mrp.GetInventory();

            var itemResults = new List<object>();
            foreach (var r in ratings)
            {
                var inv = inventory.FirstOrDefault(i => i.ItemCode == r.ItemCode);
                decimal stock = inv?.CurrentStock ?? 0;
                decimal reorder = inv?.ReorderPoint ?? 0;

                var demandResult = await _demand.AnalyzeAsync(r.ItemCode, r.BatchQuantity, stock, reorder);
                var rccpResult = await _rccp.CheckCapacityAsync(r.MachineGroup, r.UtilizationTarget, r.ShiftPerDay);
                var crpResult = await _crp.PlanCapacityAsync(r.MachineGroup, r.ProcessingTime, r.SetupTime, r.BatchQuantity);
                var sfcResult = await _sfc.ExecuteAsync(r.ItemCode, r.BatchQuantity, r.MachineGroup, r.CostPerUnit);

                itemResults.Add(new {
                    itemCode = r.ItemCode,
                    description = r.Description,
                    machineGroup = r.MachineGroup,
                    demand = demandResult,
                    rccp = rccpResult,
                    crp = crpResult,
                    sfc = sfcResult
                });
            }

            return new {
                totalWorkOrders = workOrders.Count,
                cycleDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                items = itemResults
            };
        }
    }
}
