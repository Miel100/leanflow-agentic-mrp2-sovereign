using System.Threading.Tasks;

namespace LeanFlow.Application.Agents
{
    public class CRPAgent
    {
        public async Task<string> PlanCapacityAsync(string machineGroup, decimal processingTime, decimal setupTime, int batchQty)
        {
            decimal totalTime = (processingTime + setupTime) * batchQty;
            return $"Machine: {machineGroup} | Processing: {processingTime}h | Setup: {setupTime}h | Batch: {batchQty} units | Total time: {totalTime}h";
        }
    }
}
