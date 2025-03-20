using System;
using System.Collections.Generic;

namespace IGTSQLHealthAI.Models
{
    public class DatabaseInfo
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CompatibilityLevel { get; set; }
        public double UsedSpaceMB { get; set; }
        public double DataSizeMB { get; set; }
        public double LogSizeMB { get; set; }
        public string RecoveryModel { get; set; }
        public string PageVerify { get; set; }
        public bool IsReadOnly { get; set; }
        public int TableCount { get; set; }
        public DateTime? LastAccessed { get; set; }
        public bool SizeCalculationFailed { get; set; }

        public string FormattedSize => SizeCalculationFailed 
            ? "Size unavailable" 
            : $"Used Space: {UsedSpaceMB:N1} MB (Data: {DataSizeMB:N1} MB, Log: {LogSizeMB:N1} MB)";

        public string VitalInfo
        {
            get
            {
                var info = new List<string>();
                
                if (!string.IsNullOrEmpty(RecoveryModel))
                    info.Add($"Recovery: {RecoveryModel}");
                
                if (IsReadOnly)
                    info.Add("Read-Only");

                info.Add($"Compatibility: {CompatibilityLevel}");
                
                if (TableCount > 0)
                    info.Add($"Tables: {TableCount}");
                
                if (LastAccessed.HasValue)
                    info.Add($"Last Access: {LastAccessed.Value:g}");
                
                return string.Join(" | ", info);
            }
        }
    }
}
