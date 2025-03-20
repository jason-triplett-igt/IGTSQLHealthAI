using System;

namespace IGTSQLHealthAI.Models
{
    public class InstanceData
    {
        public string Scope { get; set; }
        public string ConfigItem { get; set; }
        public string RecommendedValue { get; set; }
        public string ActualValue { get; set; }
        public DateTime TimeRun { get; set; }
    }
}
