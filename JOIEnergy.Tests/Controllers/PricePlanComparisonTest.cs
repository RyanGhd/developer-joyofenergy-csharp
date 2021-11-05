using System;
using System.Collections.Generic;
using System.Linq;
using JOIEnergy.Controllers;
using JOIEnergy.Domain;
using JOIEnergy.Enums;
using JOIEnergy.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace JOIEnergy.Tests.Controllers
{
    public class PricePlanComparisonTestFixture
    {
        public PricePlanComparisonTestFixture()
        {
            AccountService = new Mock<IAccountService>();
            PricePlanService = new Mock<IPricePlanService>();
        }

        public Mock<IAccountService> AccountService { get; }

        public Mock<IPricePlanService> PricePlanService { get; }

        public IList<PricePlan> GetPricePlans()
        {
            return new List<PricePlan>() {
                new PricePlan() { EnergySupplier = Supplier.DrEvilsDarkEnergy, UnitRate = 10, PeakTimeMultiplier = NoMultipliers() },
                new PricePlan() { EnergySupplier = Supplier.TheGreenEco, UnitRate = 2, PeakTimeMultiplier = NoMultipliers() },
                new PricePlan() { EnergySupplier = Supplier.PowerForEveryone, UnitRate = 1, PeakTimeMultiplier = NoMultipliers() }
            };
        }

        public Dictionary<string, decimal> GetConsumptionCosts()
        {
            return new Dictionary<string, decimal>()
            {
                { Supplier.DrEvilsDarkEnergy.ToString(), 12M },
                { Supplier.PowerForEveryone.ToString(), 13M },
                { Supplier.TheGreenEco.ToString(), 14M },
            };
        }

        public List<PeakTimeMultiplier> NoMultipliers()
        {
            return new List<PeakTimeMultiplier>();
        }

        public PricePlanComparatorController GetSut()
        {
            return new PricePlanComparatorController(PricePlanService.Object, AccountService.Object);
        }
    }

    public class PricePlanComparisonTest
    {
        private IMeterReadingService meterReadingService;
        private PricePlanComparatorController controller;
        private Dictionary<string, Supplier> smartMeterToPricePlanAccounts = new Dictionary<string, Supplier>();
        private static String SMART_METER_ID = "smart-meter-id";

        public PricePlanComparisonTest()
        {
            var readings = new Dictionary<string, List<JOIEnergy.Domain.ElectricityReading>>();
            var meterReadingServiceMock = new Mock<IMeterReadingService>(); //(new MeterReadingService(readings));
            meterReadingService = meterReadingServiceMock.Object;


            var pricePlanServiceMock = new Mock<IPricePlanService>(); // new PricePlanService(pricePlans, meterReadingService);

            var accountServiceMock = new Mock<IAccountService>(); // AccountService(smartMeterToPricePlanAccounts);
            controller = new PricePlanComparatorController(pricePlanServiceMock.Object, accountServiceMock.Object);
        }

        [Theory]
        [InlineData("x")]
        [InlineData("y")]
        [InlineData("z")]
        public void ShouldCalculateCostForMeterReadingsForEveryPricePlan(string smartMeterId)
        {
            //arrange 
            var fixture = new PricePlanComparisonTestFixture();

            var consumptionCosts = fixture.GetConsumptionCosts();

            fixture.PricePlanService
                .Setup(s =>
                    s.GetConsumptionCostOfElectricityReadingsForEachPricePlan(It.Is<string>(val => val.Equals(smartMeterId))))
                     .Returns(consumptionCosts);

            var sut = fixture.GetSut();

            //act 
            var result = (sut.CalculatedCostForEachPricePlan(smartMeterId) as dynamic).Value;
            var actualCosts = ((JObject)result).ToObject<Dictionary<string, decimal>>();

            //assert 
            Assert.Equal(consumptionCosts.Count, actualCosts.Count);

            foreach (var (key, value) in consumptionCosts)
            {
                Assert.Equal(value, actualCosts["" + key], 3);
            }
        }

        [Theory]
        [InlineData("x")]
        public void ShouldRecommendCheapestPricePlansNoLimitForMeterUsage(string smartMeterId)
        {
            //arrange 
            var fixture = new PricePlanComparisonTestFixture();

            var consumptionCosts = fixture.GetConsumptionCosts();

            fixture.PricePlanService
                .Setup(s =>
                    s.GetConsumptionCostOfElectricityReadingsForEachPricePlan(It.Is<string>(val => val.Equals(smartMeterId))))
                .Returns(consumptionCosts);

            var sut = fixture.GetSut();

            //act
            object result = controller.RecommendCheapestPricePlans(smartMeterId, null).Value;
            var recommendations = ((IEnumerable<KeyValuePair<string, decimal>>)result).ToList();

            //assert 
            Assert.Equal("" + Supplier.PowerForEveryone, recommendations[0].Key);
            Assert.Equal("" + Supplier.TheGreenEco, recommendations[1].Key);
            Assert.Equal("" + Supplier.DrEvilsDarkEnergy, recommendations[2].Key);
            Assert.Equal(38m, recommendations[0].Value, 3);
            Assert.Equal(76m, recommendations[1].Value, 3);
            Assert.Equal(380m, recommendations[2].Value, 3);
            Assert.Equal(3, recommendations.Count);

            //=========================
            //meterReadingService.StoreReadings(SMART_METER_ID, new List<ElectricityReading>() {
            //    new ElectricityReading() { Time = DateTime.Now.AddMinutes(-30), Reading = 35m },
            //    new ElectricityReading() { Time = DateTime.Now, Reading = 3m }
            //});
        }

        [Fact]
        public void ShouldRecommendLimitedCheapestPricePlansForMeterUsage()
        {
            meterReadingService.StoreReadings(SMART_METER_ID, new List<ElectricityReading>() {
                new ElectricityReading() { Time = DateTime.Now.AddMinutes(-45), Reading = 5m },
                new ElectricityReading() { Time = DateTime.Now, Reading = 20m }
            });

            object result = controller.RecommendCheapestPricePlans(SMART_METER_ID, 2).Value;
            var recommendations = ((IEnumerable<KeyValuePair<string, decimal>>)result).ToList();

            Assert.Equal("" + Supplier.PowerForEveryone, recommendations[0].Key);
            Assert.Equal("" + Supplier.TheGreenEco, recommendations[1].Key);
            Assert.Equal(16.667m, recommendations[0].Value, 3);
            Assert.Equal(33.333m, recommendations[1].Value, 3);
            Assert.Equal(2, recommendations.Count);
        }

        [Fact]
        public void ShouldRecommendCheapestPricePlansMoreThanLimitAvailableForMeterUsage()
        {
            meterReadingService.StoreReadings(SMART_METER_ID, new List<ElectricityReading>() {
                new ElectricityReading() { Time = DateTime.Now.AddMinutes(-30), Reading = 35m },
                new ElectricityReading() { Time = DateTime.Now, Reading = 3m }
            });

            object result = controller.RecommendCheapestPricePlans(SMART_METER_ID, 5).Value;
            var recommendations = ((IEnumerable<KeyValuePair<string, decimal>>)result).ToList();

            Assert.Equal(3, recommendations.Count);
        }

        [Fact]
        public void GivenNoMatchingMeterIdShouldReturnNotFound()
        {
            var fixture = new PricePlanComparisonTestFixture();
            fixture.PricePlanService
                .Setup(m => m.GetConsumptionCostOfElectricityReadingsForEachPricePlan(It.IsAny<string>()))
                .Returns(new Dictionary<string, decimal>());

            Assert.Equal(404, ((ObjectResult)controller.CalculatedCostForEachPricePlan("not-found")).StatusCode);
        }


    }
}
