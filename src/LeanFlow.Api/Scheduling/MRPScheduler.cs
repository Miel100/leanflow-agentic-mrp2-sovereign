using LeanFlow.Application.Services;
using System.Threading.Tasks;

namespace LeanFlow.Api.Scheduling
{
    public class MRPScheduler
    {
        private readonly LeanMRP2Service _mrp;
        private readonly WatchdogService _watchdog;

        public MRPScheduler(LeanMRP2Service mrp, WatchdogService watchdog)
        {
            _mrp = mrp;
            _watchdog = watchdog;
        }

        public async Task RunDailyMRPCycleAsync()
        {
            var result = _mrp.RunMRP2();
            System.Console.WriteLine($"[SCHEDULER] Daily MRP cycle completed. Work orders: {result.TotalWorkOrdersPlanned}, Cost: ${result.TotalPlannedCost:N0}, Exceptions: {result.TotalExceptions}");
        }

        public async Task RunWatchdogCheckAsync()
        {
            var result = await _watchdog.RunCheckAsync();
            System.Console.WriteLine($"[WATCHDOG] Check completed. Status: {result.Status}, Alerts: {result.AlertsGenerated}");
        }
    }
}
