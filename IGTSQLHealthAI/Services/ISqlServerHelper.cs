using IGTSQLHealthAI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace IGTSQLHealthAI.Services
{
    public interface ISqlServerHelper
    {
        string ConnectionString { get; }
        Task<(bool Success, string ErrorMessage)> TestConnectionAsync();
        Task<DataTable> ExecuteQueryAsync(string query, Dictionary<string, object> parameters = null);
        Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object> parameters = null);
        Task<object> ExecuteScalarAsync(string query, Dictionary<string, object> parameters = null);
        Task<List<T>> ExecuteQueryAsync<T>(string query, Func<IDataReader, T> mapper, Dictionary<string, object> parameters = null);
        Task ExecuteMultipleResultSetsAsync(string query);
        Task<List<InstanceData>> GetInstanceDataAsync();
        Task<List<TopSP>> GetTopSPsAsync();
        Task<List<StatsFrag>> GetStatsFragsAsync();
        Task<List<IndexUsage>> GetIndexUsagesAsync();
        Task<List<MissingIndex>> GetMissingIndexesAsync();
        Task<List<AvgIO>> GetAvgIOsAsync();
        Task<List<SQLWait>> GetSQLWaitsAsync();
        Task ExecuteSuperPerfAsync();
    }
}
