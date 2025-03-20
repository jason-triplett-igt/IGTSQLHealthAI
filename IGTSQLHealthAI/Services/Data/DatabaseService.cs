using IGTSQLHealthAI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IGTSQLHealthAI.Services.Data
{
    public class DatabaseService : IDatabaseService
    {
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(ILogger<DatabaseService> logger = null)
        {
            _logger = logger;
        }

        public async Task<string> GetServerStatusAsync(ISqlServerHelper helper)
        {
            try
            {
                const string query = @"
                    SELECT SERVERPROPERTY('ProductVersion') AS Version,
                           SERVERPROPERTY('Edition') AS Edition,
                           SERVERPROPERTY('ProductLevel') AS ServicePack,
                           SERVERPROPERTY('ProductUpdateLevel') AS CULevel,
                           SERVERPROPERTY('ProductBuildType') AS BuildType,
                           @@SERVERNAME AS ServerName";

                var result = await helper.ExecuteQueryAsync(query);
                if (result.Rows.Count > 0)
                {
                    var row = result.Rows[0];
                    string cuLevel = row["CULevel"] != DBNull.Value ? row["CULevel"].ToString() : "";
                    string buildType = row["BuildType"] != DBNull.Value ? row["BuildType"].ToString() : "";
                    
                    return $"Server: {row["ServerName"]}, Version: {row["Version"]}, " +
                           $"Edition: {row["Edition"]}, SP: {row["ServicePack"]}" +
                           (!string.IsNullOrEmpty(cuLevel) ? $", CU: {cuLevel}" : "") +
                           (!string.IsNullOrEmpty(buildType) ? $" ({buildType})" : "");
                }
                return "Server information unavailable";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving server status");
                throw;
            }
        }

        public async Task<List<DatabaseInfo>> GetDatabaseInfoAsync(ISqlServerHelper helper)
        {
            List<DatabaseInfo> databases = new List<DatabaseInfo>();
            
            try
            {
                // CTE Query for database information with file sizes aggregated by database
                const string query = @"
                    SELECT
                        d.name,
                        d.state_desc AS Status,
                        d.create_date AS CreatedDate,
                        d.compatibility_level AS CompatibilityLevel,
                        CONVERT(DECIMAL(18,2), SUM(f.size * 8 / 1024.0)) AS UsedSpaceMB,
                        CONVERT(DECIMAL(18,2), SUM(IIF(f.type = 0, f.size, 0) * 8 / 1024.0)) AS DataSizeMB,
                        CONVERT(DECIMAL(18,2), SUM(IIF(f.type = 1, f.size, 0) * 8 / 1024.0)) AS LogSizeMB,
                        d.recovery_model_desc AS RecoveryModel,
                        d.page_verify_option_desc AS PageVerify,
                        MAX(IIF(f.is_read_only = 1, 1, 0)) AS IsReadOnly,
                        (SELECT MAX(last_user_seek) FROM sys.dm_db_index_usage_stats WHERE database_id = d.database_id) AS LastAccessed
                    FROM sys.databases d
                    LEFT JOIN sys.master_files f ON d.database_id = f.database_id
                    GROUP BY d.name, d.state_desc, d.create_date, d.compatibility_level, d.recovery_model_desc, d.page_verify_option_desc, d.database_id
                    ORDER BY d.name";

                var results = await helper.ExecuteQueryAsync<DatabaseInfo>(query, reader => new DatabaseInfo
                {
                    Name = reader["name"].ToString(),
                    Status = reader["Status"].ToString(),
                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                    CompatibilityLevel = Convert.ToInt32(reader["CompatibilityLevel"]),
                    UsedSpaceMB = reader["UsedSpaceMB"] == DBNull.Value ? 0 : Convert.ToDouble(reader["UsedSpaceMB"]),
                    DataSizeMB = reader["DataSizeMB"] == DBNull.Value ? 0 : Convert.ToDouble(reader["DataSizeMB"]),
                    LogSizeMB = reader["LogSizeMB"] == DBNull.Value ? 0 : Convert.ToDouble(reader["LogSizeMB"]),
                    RecoveryModel = reader["RecoveryModel"].ToString(),
                    PageVerify = reader["PageVerify"].ToString(),
                    IsReadOnly = reader["IsReadOnly"] != DBNull.Value && Convert.ToInt32(reader["IsReadOnly"]) == 1,
                    LastAccessed = reader["LastAccessed"] != DBNull.Value ? Convert.ToDateTime(reader["LastAccessed"]) : (DateTime?)null
                });

                return results;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading databases with detailed query. Trying simpler query.");
                
                try
                {
                    // Fallback to simpler query
                    const string fallbackQuery = @"
                        SELECT 
                            d.name, 
                            d.state_desc AS Status,
                            d.create_date AS CreatedDate,
                            d.compatibility_level AS CompatibilityLevel,
                            CONVERT(DECIMAL(18,2), SUM(mf.size) * 8 / 1024.0) AS UsedSpaceMB,
                            d.recovery_model_desc AS RecoveryModel
                        FROM sys.databases d
                        LEFT JOIN sys.master_files mf ON d.database_id = mf.database_id
                        GROUP BY d.name, d.state_desc, d.create_date, d.compatibility_level, d.recovery_model_desc
                        ORDER BY name";

                    var results = await helper.ExecuteQueryAsync<DatabaseInfo>(fallbackQuery, reader => new DatabaseInfo
                    {
                        Name = reader["name"].ToString(),
                        Status = reader["Status"].ToString(),
                        CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                        CompatibilityLevel = Convert.ToInt32(reader["CompatibilityLevel"]),
                        UsedSpaceMB = reader["UsedSpaceMB"] == DBNull.Value ? 0 : Convert.ToDouble(reader["UsedSpaceMB"]),
                        RecoveryModel = reader["RecoveryModel"] == DBNull.Value ? "Unknown" : reader["RecoveryModel"].ToString(),
                        SizeCalculationFailed = false
                    });

                    return results;
                }
                catch (Exception innerEx)
                {
                    _logger?.LogError(innerEx, "Error loading databases with simplified query. Using basic query.");
                    
                    // Last resort - most basic query
                    const string basicQuery = @"
                        SELECT name, 
                               state_desc AS Status,
                               create_date AS CreatedDate,
                               compatibility_level AS CompatibilityLevel
                        FROM sys.databases
                        ORDER BY name";

                    var results = await helper.ExecuteQueryAsync<DatabaseInfo>(basicQuery, reader => new DatabaseInfo
                    {
                        Name = reader["name"].ToString(),
                        Status = reader["Status"].ToString(),
                        CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                        CompatibilityLevel = Convert.ToInt32(reader["CompatibilityLevel"]),
                        UsedSpaceMB = 0,
                        SizeCalculationFailed = true
                    });

                    return results;
                }
            }
        }
    }
}
