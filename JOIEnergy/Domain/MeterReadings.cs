using System;
using System.Collections.Generic;
using System.Linq;

namespace JOIEnergy.Domain
{
    public class MeterReadings
    {
        public string SmartMeterId { get; set; }
        public List<ElectricityReading> ElectricityReadings { get; set; }

        public bool IsMeterReadingsValid(MeterReadings meterReadings)
        {
            String smartMeterId = meterReadings.SmartMeterId;
            List<ElectricityReading> electricityReadings = meterReadings.ElectricityReadings;
            return smartMeterId != null && smartMeterId.Any()
                                        && electricityReadings != null && electricityReadings.Any();
        }
    }
}
