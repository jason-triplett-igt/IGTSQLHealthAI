using System;

namespace IGTSQLHealthAI.Models
{
    public class StatsFrag
    {
        public string DbName { get; set; }
        public string SchemaName { get; set; }
        public string TableName { get; set; }
        public string IndexName { get; set; }
        public long PageCount { get; set; }
        public decimal PercentAvgFragmentation { get; set; }
        public long RowMods { get; set; }
        public long RowCount { get; set; }
        public decimal PercentRowMods { get; set; }
        public DateTime LastStatisticsUpdate { get; set; }
        public DateTime TimeRun { get; set; }
    }
}
