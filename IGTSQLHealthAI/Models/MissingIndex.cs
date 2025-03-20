using System;

namespace IGTSQLHealthAI.Models
{
    public class MissingIndex
    {
        public string DbName { get; set; }
        public double IndexImpact { get; set; }
        public string FullObjectName { get; set; }
        public string TableName { get; set; }
        public string EqualityColumns { get; set; }
        public string InequalityColumns { get; set; }
        public string IncludedColumns { get; set; }
        public long Compiles { get; set; }
        public long Seeks { get; set; }
        public DateTime LastUserSeek { get; set; }
        public double UserCost { get; set; }
        public double UserImpact { get; set; }
        public DateTime TimeRun { get; set; }
    }
}
