using System;

namespace IGTSQLHealthAI.Models
{
    public class ProcedureMetric
    {
        public string Name { get; set; }
        public int ExecutionCount { get; set; }
        public double CpuTimeSeconds { get; set; }
        public double DurationSeconds { get; set; }
        public long LogicalReads { get; set; }
        public long LogicalWrites { get; set; }
        public DateTime LastExecuted { get; set; }

        public string FormattedDuration => $"{DurationSeconds:N1} sec";
        public string FormattedCpuTime => $"{CpuTimeSeconds:N1} sec";
        public string FormattedLastExecuted => LastExecuted == DateTime.MinValue ? "Never" : LastExecuted.ToString("g");
    }
}
