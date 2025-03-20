using System;

namespace IGTSQLHealthAI.Models
{
    public class AvgIO
    {
        public string DbName { get; set; }
        public string DbFileName { get; set; }
        public long IoStallReadMs { get; set; }
        public long NumOfReads { get; set; }
        public long AvgReadStallMs { get; set; }
        public long IoStallWriteMs { get; set; }
        public long NumOfWrites { get; set; }
        public long AvgWriteStallMs { get; set; }
        public long IoStalls { get; set; }
        public long TotalIo { get; set; }
        public long AvgIoStallMs { get; set; }
        public DateTime TimeRun { get; set; }
    }
}
