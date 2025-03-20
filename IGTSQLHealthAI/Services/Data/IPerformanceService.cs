using IGTSQLHealthAI.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IGTSQLHealthAI.Services.Data
{
    public interface IPerformanceService
    {
        Task<List<PerformanceMetric>> GetGeneralMetricsAsync(ISqlServerHelper helper);
        Task<List<PerformanceMetric>> GetDiskMetricsAsync(ISqlServerHelper helper);
        Task<List<ProcedureMetric>> GetTopProceduresAsync(ISqlServerHelper helper);
    }
}
