using System.Threading.Tasks;

namespace LeanFlow.Application.Agents
{
    public class DemandAgent
    {
        public async Task<string> AnalyzeAsync(string input)
        {
            return "DemandAgent: Analyzed demand for - " + input + ". Forecast generated with seasonal adjustments.";
        }
    }
}
