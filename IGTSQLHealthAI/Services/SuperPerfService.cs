using IGTSQLHealthAI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IGTSQLHealthAI.Services
{
    public class SuperPerfService : ISuperPerfService
    {
        private readonly ILogger<SuperPerfService> _logger;

        public SuperPerfService(ILogger<SuperPerfService> logger = null)
        {
            _logger = logger;
        }

        public async Task<IEnumerable<InstanceData>> GetInstanceDataAsync(ISqlServerHelper helper)
        {
            try
            {
                return await helper.GetInstanceDataAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting Instance Data");
                return new List<InstanceData>();
            }
        }

        public async Task<IEnumerable<TopSP>> GetTopSPsAsync(ISqlServerHelper helper)
        {
            try
            {
                return await helper.GetTopSPsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting TopSPs");
                return new List<TopSP>();
            }
        }

        public async Task<IEnumerable<StatsFrag>> GetStatsFragsAsync(ISqlServerHelper helper)
        {
            try
            {
                return await helper.GetStatsFragsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting StatsFrags");
                return new List<StatsFrag>();
            }
        }

        public async Task<IEnumerable<IndexUsage>> GetIndexUsagesAsync(ISqlServerHelper helper)
        {
            try
            {
                return await helper.GetIndexUsagesAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting IndexUsages");
                return new List<IndexUsage>();
            }
        }

        public async Task<IEnumerable<MissingIndex>> GetMissingIndexesAsync(ISqlServerHelper helper)
        {
            try
            {
                return await helper.GetMissingIndexesAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting MissingIndexes");
                return new List<MissingIndex>();
            }
        }

        public async Task<IEnumerable<AvgIO>> GetAvgIOsAsync(ISqlServerHelper helper)
        {
            try
            {
                return await helper.GetAvgIOsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting AvgIOs");
                return new List<AvgIO>();
            }
        }

        public async Task<IEnumerable<SQLWait>> GetSQLWaitsAsync(ISqlServerHelper helper)
        {
            try
            {
                return await helper.GetSQLWaitsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting SQLWaits");
                return new List<SQLWait>();
            }
        }
        
        // New Method
        public async Task ExecuteSuperPerfAsync(ISqlServerHelper helper)
        {
            try
            {
                await helper.ExecuteSuperPerfAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing SuperPerf Queries");
            }
        }
    }
}
