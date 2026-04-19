using System.Threading.Tasks;

namespace LeanFlow.Application.Agents
{
    public class SFCAgent
    {
        public async Task<string> ExecuteAsync(string input)
        {
            return "SFCAgent: Shop Floor Control executed using Rating File for - " + input + ". Work orders issued.";
        }
    }
}
