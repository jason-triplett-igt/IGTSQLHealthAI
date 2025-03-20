using System;

namespace IGTSQLHealthAI.Models
{
    public class SQLWait
    {
        public string WaitType { get; set; }
        public decimal WaitS { get; set; }
        public decimal ResourceS { get; set; }
        public decimal SignalS { get; set; }
        public long WaitCount { get; set; }
        public decimal Percentage { get; set; }
        public decimal AvgWaitS { get; set; }
        public decimal AvgResS { get; set; }
        public decimal AvgSigS { get; set; }
        public DateTime TimeRun { get; set; }
    }
}
