using System;

namespace AzerothCoreLauncher
{
    public class HealthDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double AuthMemoryMB { get; set; }
        public double AuthCpuPercent { get; set; }
        public double WorldMemoryMB { get; set; }
        public double WorldCpuPercent { get; set; }
    }
}
