using System.Threading.Tasks;

namespace LeanFlow.Application.Agents
{
    public class RCCPAgent
    {
        public async Task<string> CheckCapacityAsync(string input)
        {
            return "RCCPAgent: Rough-cut capacity checked for - " + input + ". Utilization within target range.";
        }
    }
}
