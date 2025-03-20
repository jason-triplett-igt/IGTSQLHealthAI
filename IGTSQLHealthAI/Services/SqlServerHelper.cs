using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IGTSQLHealthAI.Models;
using System.IO;
using System.Reflection;

namespace IGTSQLHealthAI.Services
{


    public class SqlServerHelper : ISqlServerHelper
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlServerHelper> _logger;

        public string ConnectionString => _connectionString;

        public SqlServerHelper(IConfiguration configuration, ILogger<SqlServerHelper> logger = null)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        public SqlServerHelper(string connectionString, ILogger<SqlServerHelper> logger = null)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<DataTable> ExecuteQueryAsync(string query, Dictionary<string, object> parameters = null)
        {
            DataTable dataTable = new DataTable();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = CreateCommand(connection, query, parameters))
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing query: {Query}", query);
                    throw;
                }
            }
            return dataTable;
        }

        public async Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object> parameters = null)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = CreateCommand(connection, query, parameters))
                    {
                        return await command.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing non-query: {Query}", query);
                    throw;
                }
            }
        }

        public async Task<object> ExecuteScalarAsync(string query, Dictionary<string, object> parameters = null)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = CreateCommand(connection, query, parameters))
                    {
                        return await command.ExecuteScalarAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing scalar query: {Query}", query);
                    throw;
                }
            }
        }

        public async Task<List<T>> ExecuteQueryAsync<T>(string query, Func<IDataReader, T> mapper, Dictionary<string, object> parameters = null)
        {
            List<T> results = new List<T>();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = CreateCommand(connection, query, parameters))
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(mapper(reader));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing mapped query: {Query}", query);
                    throw;
                }
            }
            return results;
        }

        public async Task<(bool Success, string ErrorMessage)> TestConnectionAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    
                    // Further verify we can execute a simple query
                    using (var command = new SqlCommand("SELECT @@VERSION", connection))
                    {
                        await command.ExecuteScalarAsync();
                    }
                    
                    return (true, null);
                }
                catch (SqlException ex)
                {
                    string errorMessage = FormatSqlError(ex);
                    _logger?.LogError(ex, "Failed to connect to SQL Server: {Error}", errorMessage);
                    return (false, errorMessage);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to connect to SQL Server");
                    return (false, ex.Message);
                }
            }
        }

        public async Task ExecuteMultipleResultSetsAsync(string query)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = CreateCommand(connection, query, null)) // Parameters not supported for multi-result sets in this example
                    {
                        command.CommandTimeout = 300; // Set command timeout to 5 minutes

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            // Process each result set
                            _instanceData = await ReadInstanceDataAsync(reader);
                            if (await reader?.NextResultAsync())
                            {
                                _topSPs = await ReadTopSPsAsync(reader);
                                if (await reader?.NextResultAsync())
                                {
                                    _statsFrags = await ReadStatsFragsAsync(reader);
                                    if (await reader?.NextResultAsync())
                                    {
                                        _indexUsages = await ReadIndexUsagesAsync(reader);
                                        if (await reader?.NextResultAsync())
                                        {
                                            _missingIndexes = await ReadMissingIndexesAsync(reader);
                                            if (await reader?.NextResultAsync())
                                            {
                                                _avgIOs = await ReadAvgIOsAsync(reader);
                                                if (await reader?.NextResultAsync())
                                                {
                                                    _sqlWaits = await ReadSQLWaitsAsync(reader);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing multiple result sets query: {Query}", query);
                    throw;
                }
            }
        }

        private List<InstanceData> _instanceData;
        private List<TopSP> _topSPs;
        private List<StatsFrag> _statsFrags;
        private List<IndexUsage> _indexUsages;
        private List<MissingIndex> _missingIndexes;
        private List<AvgIO> _avgIOs;
        private List<SQLWait> _sqlWaits;

        private async Task<List<InstanceData>> ReadInstanceDataAsync(SqlDataReader reader)
        {
            List<InstanceData> results = new List<InstanceData>();
            while (await reader?.ReadAsync())
            {
                results.Add(new InstanceData
                {
                    TimeRun = reader["time_run"] != DBNull.Value ? (DateTime)reader["time_run"] : DateTime.MinValue,
                    Scope = reader["scope"] != DBNull.Value ? reader["scope"].ToString() : string.Empty,
                    ConfigItem = reader["config_item"] != DBNull.Value ? reader["config_item"].ToString() : string.Empty,
                    RecommendedValue = reader["recommended_value"] != DBNull.Value ? reader["recommended_value"].ToString() : string.Empty,
                    ActualValue = reader["actual_value"] != DBNull.Value ? reader["actual_value"].ToString() : string.Empty
                });
            }
            return results;
        }

        private async Task<List<TopSP>> ReadTopSPsAsync(SqlDataReader reader)
        {
            List<TopSP> results = new List<TopSP>();
            while (await reader.ReadAsync())
            {
                results.Add(new TopSP
                {
                    TimeRun = reader["time_run"] != DBNull.Value ? (DateTime)reader["time_run"] : DateTime.MinValue,
                    DbName = reader["db_name"] != DBNull.Value ? reader["db_name"].ToString() : string.Empty,
                    SpName = reader["sp_name"] != DBNull.Value ? reader["sp_name"].ToString() : string.Empty,
                    AvgPhysicalReads = reader["avg_physical_reads"] != DBNull.Value ? (long)reader["avg_physical_reads"] : 0,
                    AvgLogicalReads = reader["avg_logical_reads"] != DBNull.Value ? (long)reader["avg_logical_reads"] : 0,
                    AvgCpuTime = reader["avg_cpu_time"] != DBNull.Value ? (long)reader["avg_cpu_time"] : 0,
                    ExecutionCount = reader["execution_count"] != DBNull.Value ? (long)reader["execution_count"] : 0,
                    TotalElapsedTime = reader["total_elapsed_time"] != DBNull.Value ? (long)reader["total_elapsed_time"] : 0,
                    AvgElapsedTimeMs = reader["avg_elapsed_time_ms"] != DBNull.Value ? (long)reader["avg_elapsed_time_ms"] : 0,
                    LastElapsedTimeMs = reader["last_elapsed_time_ms"] != DBNull.Value ? (long)reader["last_elapsed_time_ms"] : 0,
                    WhenPlanCached = reader["when_plan_cached"] != DBNull.Value ? (DateTime)reader["when_plan_cached"] : DateTime.MinValue
                });
            }
            return results;
        }

        private async Task<List<StatsFrag>> ReadStatsFragsAsync(SqlDataReader reader)
        {
            List<StatsFrag> results = new List<StatsFrag>();
            while (await reader.ReadAsync())
            {
                results.Add(new StatsFrag
                {
                    TimeRun = reader["time_run"] != DBNull.Value ? (DateTime)reader["time_run"] : DateTime.MinValue,
                    DbName = reader["db_name"] != DBNull.Value ? reader["db_name"].ToString() : string.Empty,
                    SchemaName = reader["schema_name"] != DBNull.Value ? reader["schema_name"].ToString() : string.Empty,
                    TableName = reader["table_name"] != DBNull.Value ? reader["table_name"].ToString() : string.Empty,
                    IndexName = reader["index_name"] != DBNull.Value ? reader["index_name"].ToString() : string.Empty,
                    PageCount = reader["page_count"] != DBNull.Value ? (long)reader["page_count"] : 0,
                    PercentAvgFragmentation = reader["percent_avg_fragmentation"] != DBNull.Value ? (decimal)reader["percent_avg_fragmentation"] : 0,
                    RowMods = reader["row_mods"] != DBNull.Value ? (long)reader["row_mods"] : 0,
                    RowCount = reader["row_count"] != DBNull.Value ? (long)reader["row_count"] : 0,
                    PercentRowMods = reader["percent_row_mods"] != DBNull.Value ? (decimal)reader["percent_row_mods"] : 0,
                    LastStatisticsUpdate = reader["last_statistics_update"] != DBNull.Value ? (DateTime)reader["last_statistics_update"] : DateTime.MinValue
                });
            }
            return results;
        }

        private async Task<List<IndexUsage>> ReadIndexUsagesAsync(SqlDataReader reader)
        {
            List<IndexUsage> results = new List<IndexUsage>();
            while (await reader.ReadAsync())
            {
                results.Add(new IndexUsage
                {
                    TimeRun = reader["time_run"] != DBNull.Value ? (DateTime)reader["time_run"] : DateTime.MinValue,
                    DbName = reader["db_name"] != DBNull.Value ? reader["db_name"].ToString() : string.Empty,
                    TableName = reader["table_name"] != DBNull.Value ? reader["table_name"].ToString() : string.Empty,
                    IndexName = reader["index_name"] != DBNull.Value ? reader["index_name"].ToString() : string.Empty,
                    PageCount = reader["page_count"] != DBNull.Value ? (long)reader["page_count"] : 0,
                    UserSeeks = reader["user_seeks"] != DBNull.Value ? (long)reader["user_seeks"] : 0,
                    UserScans = reader["user_scans"] != DBNull.Value ? (long)reader["user_scans"] : 0,
                    ScanPct = reader["scan_pct"] != DBNull.Value ? (decimal)reader["scan_pct"] : 0,
                    UserLookups = reader["user_lookups"] != DBNull.Value ? (long)reader["user_lookups"] : 0,
                    UserUpdates = reader["user_updates"] != DBNull.Value ? (long)reader["user_updates"] : 0,
                    LastUserSeek = reader["last_user_seek"] != DBNull.Value ? (DateTime)reader["last_user_seek"] : DateTime.MinValue,
                    LastUserScan = reader["last_user_scan"] != DBNull.Value ? (DateTime)reader["last_user_scan"] : DateTime.MinValue,
                    LastUserLookup = reader["last_user_lookup"] != DBNull.Value ? (DateTime)reader["last_user_lookup"] : DateTime.MinValue,
                    LastUserUpdate = reader["last_user_update"] != DBNull.Value ? (DateTime)reader["last_user_update"] : DateTime.MinValue
                });
            }
            return results;
        }

        private async Task<List<MissingIndex>> ReadMissingIndexesAsync(SqlDataReader reader)
        {
            List<MissingIndex> results = new List<MissingIndex>();
            while (await reader.ReadAsync())
            {
                results.Add(new MissingIndex
                {
                    TimeRun = reader["time_run"] != DBNull.Value ? (DateTime)reader["time_run"] : DateTime.MinValue,
                    DbName = reader["db_name"] != DBNull.Value ? reader["db_name"].ToString() : string.Empty,
                    IndexImpact = reader["index_impact"] != DBNull.Value ? Convert.ToDouble(reader["index_impact"]) : 0,
                    FullObjectName = reader["full_object_name"] != DBNull.Value ? reader["full_object_name"].ToString() : string.Empty,
                    TableName = reader["table_name"] != DBNull.Value ? reader["table_name"].ToString() : string.Empty,
                    EqualityColumns = reader["equality_columns"] != DBNull.Value ? reader["equality_columns"].ToString() : string.Empty,
                    InequalityColumns = reader["inequality_columns"] != DBNull.Value ? reader["inequality_columns"].ToString() : string.Empty,
                    IncludedColumns = reader["included_columns"] != DBNull.Value ? reader["included_columns"].ToString() : string.Empty,
                    Compiles = reader["compiles"] != DBNull.Value ? (long)reader["compiles"] : 0,
                    Seeks = reader["seeks"] != DBNull.Value ? (long)reader["seeks"] : 0,
                    LastUserSeek = reader["last_user_seek"] != DBNull.Value ? (DateTime)reader["last_user_seek"] : DateTime.MinValue,
                    UserCost = reader["user_cost"] != DBNull.Value ? Convert.ToDouble(reader["user_cost"]) : 0,
                    UserImpact = reader["user_impact"] != DBNull.Value ? Convert.ToDouble(reader["user_impact"]) : 0
                });
            }
            return results;
        }

        private async Task<List<AvgIO>> ReadAvgIOsAsync(SqlDataReader reader)
        {
            List<AvgIO> results = new List<AvgIO>();
            while (await reader.ReadAsync())
            {
                results.Add(new AvgIO
                {
                    TimeRun = reader["time_run"] != DBNull.Value ? (DateTime)reader["time_run"] : DateTime.MinValue,
                    DbName = reader["db_name"] != DBNull.Value ? reader["db_name"].ToString() : string.Empty,
                    DbFileName = reader["db_file_name"] != DBNull.Value ? reader["db_file_name"].ToString() : string.Empty,
                    IoStallReadMs = reader["io_stall_read_ms"] != DBNull.Value ? (long)reader["io_stall_read_ms"] : 0,
                    NumOfReads = reader["num_of_reads"] != DBNull.Value ? (long)reader["num_of_reads"] : 0,
                    AvgReadStallMs = reader["avg_read_stall_ms"] != DBNull.Value ? (long)reader["avg_read_stall_ms"] : 0,
                    IoStallWriteMs = reader["io_stall_write_ms"] != DBNull.Value ? (long)reader["io_stall_write_ms"] : 0,
                    NumOfWrites = reader["num_of_writes"] != DBNull.Value ? (long)reader["num_of_writes"] : 0,
                    AvgWriteStallMs = reader["avg_write_stall_ms"] != DBNull.Value ? (long)reader["avg_write_stall_ms"] : 0,
                    IoStalls = reader["io_stalls"] != DBNull.Value ? (long)reader["io_stalls"] : 0,
                    TotalIo = reader["total_io"] != DBNull.Value ? (long)reader["total_io"] : 0,
                    AvgIoStallMs = reader["avg_io_stall_ms"] != DBNull.Value ? (long)reader["avg_io_stall_ms"] : 0
                });
            }
            return results;
        }

        private async Task<List<SQLWait>> ReadSQLWaitsAsync(SqlDataReader reader)
        {
            List<SQLWait> results = new List<SQLWait>();
            while (await reader.ReadAsync())
            {
                results.Add(new SQLWait
                {
                    TimeRun = reader["time_run"] != DBNull.Value ? (DateTime)reader["time_run"] : DateTime.MinValue,
                    WaitType = reader["wait_type"] != DBNull.Value ? reader["wait_type"].ToString() : string.Empty,
                    WaitS = reader["wait_s"] != DBNull.Value ? (decimal)reader["wait_s"] : 0,
                    ResourceS = reader["resource_s"] != DBNull.Value ? (decimal)reader["resource_s"] : 0,
                    SignalS = reader["signal_s"] != DBNull.Value ? (decimal)reader["signal_s"] : 0,
                    WaitCount = reader["wait_count"] != DBNull.Value ? (long)reader["wait_count"] : 0,
                    Percentage = reader["percentage"] != DBNull.Value ? (decimal)reader["percentage"] : 0,
                    AvgWaitS = reader["avg_wait_s"] != DBNull.Value ? (decimal)reader["avg_wait_s"] : 0,
                    AvgResS = reader["avg_res_s"] != DBNull.Value ? (decimal)reader["avg_res_s"] : 0,
                    AvgSigS = reader["avg_sig_s"] != DBNull.Value ? (decimal)reader["avg_sig_s"] : 0
                });
            }
            return results;
        }

        // Add these methods
        public async Task<List<InstanceData>> GetInstanceDataAsync()
        {
            return _instanceData;
        }

        public async Task<List<TopSP>> GetTopSPsAsync()
        {
            return _topSPs;
        }

        public async Task<List<StatsFrag>> GetStatsFragsAsync()
        {
            return _statsFrags;
        }

        public async Task<List<IndexUsage>> GetIndexUsagesAsync()
        {
            return _indexUsages;
        }

        public async Task<List<MissingIndex>> GetMissingIndexesAsync()
        {
            return _missingIndexes;
        }

        public async Task<List<AvgIO>> GetAvgIOsAsync()
        {
            return _avgIOs;
        }

        public async Task<List<SQLWait>> GetSQLWaitsAsync()
        {
            return _sqlWaits;
        }

        public async Task ExecuteSuperPerfAsync()
        {
            string sqlFilePath = Path.Combine(AppContext.BaseDirectory, "SuperPerf_1.23.sql");
            string query;

            try
            {
                query = await File.ReadAllTextAsync(sqlFilePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading SQL file: {FilePath}", sqlFilePath);
                throw; // Re-throw the exception to be handled upstream
            }

            await ExecuteMultipleResultSetsAsync(query);
        }

        private string FormatSqlError(SqlException ex)
        {
            if (ex.Number == 4060) // Cannot open database
                return $"Database access denied: {ex.Message}";
            else if (ex.Number == 18456) // Login failed
                return "Login failed. Please check your username and password.";
            else if (ex.Number == 2 || ex.Number == 53) // Connection timeout or server not found
                return "Cannot connect to server. Please verify the server name and that it's accessible from this device.";
            else if (ex.Number == 40615) // Azure firewall issue
                return "Connection blocked by firewall. If using Azure, add your IP address to the firewall rules.";
            else if (ex.Number == 10060) // Network related connection issue
                return "Network connection timed out. Check server address and network connectivity.";
            else
                return $"SQL Server Error {ex.Number}: {ex.Message}";
        }

        private SqlCommand CreateCommand(SqlConnection connection, string query, Dictionary<string, object> parameters)
        {
            SqlCommand command = new SqlCommand(query, connection);
            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
                }
            }
            return command;
        }
    }
}
