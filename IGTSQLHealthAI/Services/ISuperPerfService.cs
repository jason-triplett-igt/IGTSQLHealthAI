using IGTSQLHealthAI.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace IGTSQLHealthAI.Services
{
    public interface ISuperPerfService
    {
        Task<IEnumerable<InstanceData>> GetInstanceDataAsync(ISqlServerHelper helper);
        Task<IEnumerable<TopSP>> GetTopSPsAsync(ISqlServerHelper helper);
        Task<IEnumerable<StatsFrag>> GetStatsFragsAsync(ISqlServerHelper helper);
        Task<IEnumerable<IndexUsage>> GetIndexUsagesAsync(ISqlServerHelper helper);
        Task<IEnumerable<MissingIndex>> GetMissingIndexesAsync(ISqlServerHelper helper);
        Task<IEnumerable<AvgIO>> GetAvgIOsAsync(ISqlServerHelper helper);
        Task<IEnumerable<SQLWait>> GetSQLWaitsAsync(ISqlServerHelper helper);
        
        // New Method
        Task ExecuteSuperPerfAsync(ISqlServerHelper helper);
    }
}
