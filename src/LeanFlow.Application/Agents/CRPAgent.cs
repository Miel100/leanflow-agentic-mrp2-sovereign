using System.Threading.Tasks;

namespace LeanFlow.Application.Agents
{
    public class CRPAgent
    {
        public async Task<string> PlanCapacityAsync(string input)
        {
            return "CRPAgent: Detailed capacity planned for - " + input + ". Process times and machine groups allocated.";
        }
    }
}
