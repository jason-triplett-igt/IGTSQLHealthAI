namespace IGTSQLHealthAI.Models
{
    public class PerformanceMetric
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; } = "";

        public string FormattedValue
        {
            get
            {
                if (Unit == "ms")
                    return $"{Value:N1} {Unit}";
                else if (Name.Contains("%"))
                    return $"{Value:N1}%";
                else
                    return $"{Value:N0}";
            }
        }
    }
}
