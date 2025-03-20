using System;

namespace IGTSQLHealthAI.Models
{
    public class IndexUsage
    {
        public string DbName { get; set; }
        public string TableName { get; set; }
        public string IndexName { get; set; }
        public long PageCount { get; set; }
        public long UserSeeks { get; set; }
        public long UserScans { get; set; }
        public decimal ScanPct { get; set; }
        public long UserLookups { get; set; }
        public long UserUpdates { get; set; }
        public DateTime LastUserSeek { get; set; }
        public DateTime LastUserScan { get; set; }
        public DateTime LastUserLookup { get; set; }
        public DateTime LastUserUpdate { get; set; }
        public DateTime TimeRun { get; set; }
    }
}
