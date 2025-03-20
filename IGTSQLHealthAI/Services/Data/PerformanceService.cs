using IGTSQLHealthAI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace IGTSQLHealthAI.Services.Data
{
    public class PerformanceService : IPerformanceService
    {
        private readonly ILogger<PerformanceService> _logger;

        public PerformanceService(ILogger<PerformanceService> logger = null)
        {
            _logger = logger;
        }

        public async Task<List<PerformanceMetric>> GetGeneralMetricsAsync(ISqlServerHelper helper)
        {
            List<PerformanceMetric> metrics = new List<PerformanceMetric>();
            
            try
            {
                const string query = @"
                    SELECT 'Connected Users' AS Metric, 
                           COUNT(*) AS Value
                    FROM sys.dm_exec_sessions
                    WHERE is_user_process = 1
                    UNION ALL
                    SELECT 'Active Requests' AS Metric, 
                           COUNT(*) AS Value
                    FROM sys.dm_exec_requests
                    WHERE session_id > 50
                    UNION ALL
                    SELECT 'Buffer Cache Hit Ratio %' AS Metric, 
                           (SELECT cntr_value FROM sys.dm_os_performance_counters 
                            WHERE counter_name = 'Buffer cache hit ratio'
                            AND object_name LIKE '%Buffer Manager%') AS Value
                    UNION ALL
                    SELECT 'Page Life Expectancy (sec)' AS Metric, 
                           (SELECT cntr_value FROM sys.dm_os_performance_counters 
                            WHERE counter_name = 'Page life expectancy'
                            AND object_name LIKE '%Buffer Manager%') AS Value
                    UNION ALL
                    SELECT 'Batch Requests/sec' AS Metric, 
                           (SELECT cntr_value FROM sys.dm_os_performance_counters 
                            WHERE counter_name = 'Batch Requests/sec'
                            AND object_name LIKE '%SQL Statistics%') AS Value";

                var results = await helper.ExecuteQueryAsync<PerformanceMetric>(query, reader => new PerformanceMetric
                {
                    Name = reader["Metric"].ToString(),
                    Value = reader["Value"] == DBNull.Value ? 0 : Convert.ToDouble(reader["Value"]),
                    Category = "General"
                });

                return results;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving general performance metrics");
                
                // Return a placeholder metric
                metrics.Add(new PerformanceMetric 
                { 
                    Name = "Metrics Unavailable", 
                    Value = 0,
                    Category = "General"
                });
                
                return metrics;
            }
        }

        public async Task<List<PerformanceMetric>> GetDiskMetricsAsync(ISqlServerHelper helper)
        {
            List<PerformanceMetric> metrics = new List<PerformanceMetric>();
            
            try
            {
                const string query = @"
                    SELECT 
                        Drive = UPPER(LEFT(mf.physical_name, 1)),
                        [Read Latency (ms)] = CASE WHEN num_of_reads = 0 
                                              THEN 0 ELSE (io_stall_read_ms / num_of_reads) END,
                        [Write Latency (ms)] = CASE WHEN num_of_writes = 0 
                                               THEN 0 ELSE (io_stall_write_ms / num_of_writes) END,
                        [Overall Latency (ms)] = CASE WHEN (num_of_reads + num_of_writes) = 0 
                                             THEN 0 ELSE (io_stall / (num_of_reads + num_of_writes)) END
                    FROM 
                        sys.dm_io_virtual_file_stats(NULL, NULL) AS vfs
                    JOIN 
                        sys.master_files AS mf ON vfs.database_id = mf.database_id AND vfs.file_id = mf.file_id
                    GROUP BY 
                        UPPER(LEFT(mf.physical_name, 1)),
                        CASE WHEN num_of_reads = 0 THEN 0 ELSE (io_stall_read_ms / num_of_reads) END,
                        CASE WHEN num_of_writes = 0 THEN 0 ELSE (io_stall_write_ms / num_of_writes) END,
                        CASE WHEN (num_of_reads + num_of_writes) = 0 THEN 0 ELSE (io_stall / (num_of_reads + num_of_writes)) END
                    ORDER BY 
                        UPPER(LEFT(mf.physical_name, 1))";

                var results = await helper.ExecuteQueryAsync(query);
                
                foreach (DataRow row in results.Rows)
                {
                    string drive = row["Drive"].ToString();
                    
                    metrics.Add(new PerformanceMetric 
                    { 
                        Name = $"Drive {drive} Read Latency", 
                        Value = Convert.ToDouble(row["Read Latency (ms)"]),
                        Category = "Disk", 
                        Unit = "ms"
                    });
                    
                    metrics.Add(new PerformanceMetric 
                    { 
                        Name = $"Drive {drive} Write Latency", 
                        Value = Convert.ToDouble(row["Write Latency (ms)"]),
                        Category = "Disk", 
                        Unit = "ms"
                    });
                    
                    metrics.Add(new PerformanceMetric 
                    { 
                        Name = $"Drive {drive} Overall Latency", 
                        Value = Convert.ToDouble(row["Overall Latency (ms)"]),
                        Category = "Disk", 
                        Unit = "ms" 
                    });
                }
                
                return metrics;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving disk performance metrics");
                
                // Return a placeholder metric
                metrics.Add(new PerformanceMetric 
                { 
                    Name = "Disk Metrics Unavailable", 
                    Value = 0,
                    Category = "Disk" 
                });
                
                return metrics;
            }
        }

        public async Task<List<ProcedureMetric>> GetTopProceduresAsync(ISqlServerHelper helper)
        {
            List<ProcedureMetric> procedures = new List<ProcedureMetric>();
            
            try
            {
                const string query = @"
                    SELECT TOP 5
                        OBJECT_NAME(st.objectid, st.dbid) AS ProcedureName,
                        qs.execution_count AS ExecutionCount,
                        qs.total_worker_time / 1000000 AS TotalCpuTimeInSeconds,
                        qs.total_elapsed_time / 1000000 AS TotalDurationInSeconds,
                        qs.total_logical_reads AS TotalLogicalReads,
                        qs.total_logical_writes AS TotalLogicalWrites,
                        CAST(qs.last_execution_time AS DATETIME) AS LastExecutionTime,
                        CAST(qs.creation_time AS DATETIME) AS CachedTime
                    FROM 
                        sys.dm_exec_query_stats AS qs
                    CROSS APPLY 
                        sys.dm_exec_sql_text(qs.sql_handle) AS st
                    WHERE 
                        st.objectid IS NOT NULL
                    ORDER BY 
                        qs.total_elapsed_time DESC";

                var results = await helper.ExecuteQueryAsync<ProcedureMetric>(query, reader => new ProcedureMetric
                {
                    Name = reader["ProcedureName"] == DBNull.Value ? "Unknown" : reader["ProcedureName"].ToString(),
                    ExecutionCount = reader["ExecutionCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ExecutionCount"]),
                    CpuTimeSeconds = reader["TotalCpuTimeInSeconds"] == DBNull.Value ? 0 : Convert.ToDouble(reader["TotalCpuTimeInSeconds"]),
                    DurationSeconds = reader["TotalDurationInSeconds"] == DBNull.Value ? 0 : Convert.ToDouble(reader["TotalDurationInSeconds"]),
                    LogicalReads = reader["TotalLogicalReads"] == DBNull.Value ? 0 : Convert.ToInt64(reader["TotalLogicalReads"]),
                    LogicalWrites = reader["TotalLogicalWrites"] == DBNull.Value ? 0 : Convert.ToInt64(reader["TotalLogicalWrites"]),
                    LastExecuted = reader["LastExecutionTime"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["LastExecutionTime"])
                });

                return results;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving top procedures");
                
                // Return a placeholder procedure
                procedures.Add(new ProcedureMetric 
                { 
                    Name = "Procedure Metrics Unavailable",
                    ExecutionCount = 0,
                    DurationSeconds = 0
                });
                
                return procedures;
            }
        }
    }
}
