using System;

namespace IGTSQLHealthAI.Models
{
    public class TopSP
    {
        public string DbName { get; set; }
        public string SpName { get; set; }
        public long AvgPhysicalReads { get; set; }
        public long AvgLogicalReads { get; set; }
        public long AvgCpuTime { get; set; }
        public long ExecutionCount { get; set; }
        public long TotalElapsedTime { get; set; }
        public long AvgElapsedTimeMs { get; set; }
        public long LastElapsedTimeMs { get; set; }
        public DateTime WhenPlanCached { get; set; }
        public DateTime TimeRun { get; set; }
    }
}
