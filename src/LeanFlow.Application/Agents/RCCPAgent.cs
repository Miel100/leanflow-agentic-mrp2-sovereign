using System.Threading.Tasks;

namespace LeanFlow.Application.Agents
{
    public class RCCPAgent
    {
        public async Task<string> CheckCapacityAsync(string machineGroup, decimal utilizationTarget, int shifts)
        {
            decimal utilPct = utilizationTarget * 100;
            string status = utilizationTarget >= 0.90m ? " ⚠️ HIGH LOAD" : " ✅ OK";
            return $"Machine: {machineGroup} | Shifts: {shifts} | Target utilization: {utilPct}%{status}";
        }
    }
}
