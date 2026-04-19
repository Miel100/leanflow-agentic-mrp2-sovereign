using LeanFlow.Application.Agents;
using System.Threading.Tasks;

namespace LeanFlow.Api.Scheduling
{
    public class MRPScheduler
    {
        private readonly SupervisorAgent _supervisor;

        public MRPScheduler(SupervisorAgent supervisor)
        {
            _supervisor = supervisor;
        }

        public async Task RunDailyMRPCycleAsync()
        {
            var result = await _supervisor.RunFullMRPCycleAsync();
            Console.WriteLine("Scheduled MRP cycle completed: " + result);
        }
    }
}
