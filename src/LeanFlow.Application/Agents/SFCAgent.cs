using System;
using System.Threading.Tasks;

namespace LeanFlow.Application.Agents
{
    public class SFCAgent
    {
        public async Task<string> ExecuteAsync(string itemCode, int batchQty, string machineGroup, decimal costPerUnit)
        {
            decimal totalCost = batchQty * costPerUnit;
            string dueDate = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd");
            return $"WO issued | Item: {itemCode} | Qty: {batchQty} | Machine: {machineGroup} | Due: {dueDate} | Cost: ";
        }
    }
}
