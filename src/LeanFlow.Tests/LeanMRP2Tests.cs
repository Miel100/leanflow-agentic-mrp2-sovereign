using LeanFlow.Domain.Entities;
using LeanFlow.Application.Services;
using System.Threading.Tasks;
using Xunit;

namespace LeanFlow.Tests
{
    public class LeanMRP2Tests
    {
        [Fact]
        public void RatingFile_ShouldHaveDefaultUtilizationTarget()
        {
            var rf = new RatingFile();
            Assert.Equal(0.85m, rf.UtilizationTarget);
        }

        [Fact]
        public void RatingFile_ShouldHaveDefaultShiftPerDay()
        {
            var rf = new RatingFile();
            Assert.Equal(3, rf.ShiftPerDay);
        }

        [Fact]
        public void InventoryRecord_ShouldInitializeWithZeroStock()
        {
            var inv = new InventoryRecord();
            Assert.Equal(0, inv.CurrentStock);
        }

        [Fact]
        public void WorkOrder_ShouldHaveDefaultStatusPlanned()
        {
            var wo = new WorkOrder();
            Assert.Equal("Planned", wo.Status);
        }

        [Fact]
        public async Task LeanMRP2Service_ShouldGenerateWorkOrders()
        {
            var service = new LeanMRP2Service();
            var forecast = new DemandForecast { ItemCode = "ITEM-001", ForecastedQuantity = 500 };
            var result = await service.RunSFCAsync(forecast);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task LeanMRP2Service_ShouldReturnCorrectItemCode()
        {
            var service = new LeanMRP2Service();
            var forecast = new DemandForecast { ItemCode = "ITEM-001", ForecastedQuantity = 500 };
            var result = await service.RunSFCAsync(forecast);
            Assert.Equal("ITEM-001", result[0].ItemCode);
        }
    }
}
