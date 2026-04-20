using LeanFlow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LeanFlow.Application.Agents
{
    public class FactoryConfigAgent
    {
        private readonly HttpClient _http;
        private readonly string _groqApiKey;
        private const string GroqUrl = "https://api.groq.com/openai/v1/chat/completions";

        public FactoryConfigAgent(IHttpClientFactory factory)
        {
            _groqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? string.Empty;
            _http = factory.CreateClient();
            _http.DefaultRequestHeaders.Add("Authorization", "Bearer " + _groqApiKey);
        }

        public async Task<FactoryConfigResult> ExtractConfigAsync(string factoryDescription)
        {
            var systemPrompt = "You are a Lean MRP2 configuration expert. Extract manufacturing parameters from the user description. Return ONLY valid JSON in this exact format, no explanation, no markdown: {\"items\": [{\"itemCode\": \"ITEM-001\", \"description\": \"product name\", \"machineGroup\": \"MCH-01\", \"processingTime\": 2.5, \"setupTime\": 0.5, \"batchQuantity\": 100, \"utilizationTarget\": 0.85, \"shiftPerDay\": 2, \"costPerUnit\": 50.0, \"currentStock\": 200, \"safetyStock\": 50, \"reorderPoint\": 80, \"weeklyDemand\": 100}], \"planningHorizonWeeks\": 4, \"factorySummary\": \"one sentence summary\"}";

            var payload = new
            {
                model = "llama-3.1-8b-instant",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = factoryDescription }
                },
                max_tokens = 1024,
                temperature = 0.1
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(GroqUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new FactoryConfigResult { Error = "LLM error: " + responseJson };

            using var doc = JsonDocument.Parse(responseJson);
            var llmResponse = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            return ParseFactoryConfig(llmResponse);
        }

        private FactoryConfigResult ParseFactoryConfig(string llmJson)
        {
            try
            {
                var cleaned = llmJson
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                using var doc = JsonDocument.Parse(cleaned);
                var root = doc.RootElement;

                var result = new FactoryConfigResult
                {
                    FactorySummary = root.TryGetProperty("factorySummary", out var summary) ? summary.GetString() ?? "" : "",
                    PlanningHorizonWeeks = root.TryGetProperty("planningHorizonWeeks", out var hw) ? hw.GetInt32() : 4
                };

                if (root.TryGetProperty("items", out var items))
                {
                    int idx = 1;
                    foreach (var item in items.EnumerateArray())
                    {
                        var rf = new RatingFile
                        {
                            ItemCode = GetString(item, "itemCode", $"ITEM-{idx:D3}"),
                            Description = GetString(item, "description", "Product " + idx),
                            MachineGroup = GetString(item, "machineGroup", "MCH-01"),
                            ProcessingTime = GetDecimal(item, "processingTime", 2.0m),
                            SetupTime = GetDecimal(item, "setupTime", 0.5m),
                            BatchQuantity = GetInt(item, "batchQuantity", 100),
                            UtilizationTarget = GetDecimal(item, "utilizationTarget", 0.85m),
                            ShiftPerDay = GetInt(item, "shiftPerDay", 2),
                            CostPerUnit = GetDecimal(item, "costPerUnit", 50.0m),
                            LastUpdated = DateTime.UtcNow
                        };

                        var inv = new InventoryRecord
                        {
                            ItemCode = rf.ItemCode,
                            Description = rf.Description,
                            CurrentStock = GetDecimal(item, "currentStock", 100m),
                            SafetyStock = GetDecimal(item, "safetyStock", 20m),
                            ReorderPoint = GetDecimal(item, "reorderPoint", 40m),
                            LastUpdated = DateTime.UtcNow
                        };

                        var demand = new DemandForecast
                        {
                            ItemCode = rf.ItemCode,
                            ForecastedQuantity = GetDecimal(item, "weeklyDemand", 80m) * 4,
                            ForecastDate = DateTime.UtcNow,
                            ConfidenceLevel = 0.80m
                        };

                        result.RatingFiles.Add(rf);
                        result.InventoryRecords.Add(inv);
                        result.DemandForecasts.Add(demand);
                        idx++;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return new FactoryConfigResult { Error = "Parse error: " + ex.Message + " | Raw: " + llmJson };
            }
        }

        private string GetString(JsonElement el, string key, string def)
            => el.TryGetProperty(key, out var v) ? v.GetString() ?? def : def;

        private decimal GetDecimal(JsonElement el, string key, decimal def)
            => el.TryGetProperty(key, out var v) && v.TryGetDecimal(out var d) ? d : def;

        private int GetInt(JsonElement el, string key, int def)
            => el.TryGetProperty(key, out var v) && v.TryGetInt32(out var i) ? i : def;
    }

    public class FactoryConfigResult
    {
        public string FactorySummary { get; set; } = string.Empty;
        public int PlanningHorizonWeeks { get; set; } = 4;
        public List<RatingFile> RatingFiles { get; set; } = new();
        public List<InventoryRecord> InventoryRecords { get; set; } = new();
        public List<DemandForecast> DemandForecasts { get; set; } = new();
        public string? Error { get; set; }
        public bool HasError => !string.IsNullOrEmpty(Error);
    }
}