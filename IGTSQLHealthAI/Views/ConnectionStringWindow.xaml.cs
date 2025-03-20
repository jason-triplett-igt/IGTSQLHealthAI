using System;
using Microsoft.Maui.Controls;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Data;

namespace IGTSQLHealthAI.Views
{
    public partial class ConnectionStringWindow : ContentPage
    {
        // Default constructor for new connections
        public ConnectionStringWindow()
        {
            InitializeComponent();
            BindingContext = this;
        }
        
        // Constructor that accepts an existing connection string to edit
        public ConnectionStringWindow(string existingConnectionString)
        {
            InitializeComponent();
            
            if (!string.IsNullOrEmpty(existingConnectionString))
            {
                try
                {
                    // Parse the existing connection string into its components
                    var builder = new SqlConnectionStringBuilder(existingConnectionString);
                    
                    // Set the properties for binding
                    DataSource = builder.DataSource;
                    InitialCatalog = builder.InitialCatalog;
                    IntegratedSecurity = builder.IntegratedSecurity;
                    Encrypt = builder.Encrypt;
                    TrustServerCertificate = builder.TrustServerCertificate;
                    
                    if (!builder.IntegratedSecurity)
                    {
                        UserID = builder.UserID;
                        Password = builder.Password;
                    }
                    
                    ConnectionString = existingConnectionString;
                }
                catch (Exception ex)
                {
                    // If parsing fails, just leave the form empty
                    Console.WriteLine($"Error parsing connection string: {ex.Message}");
                }
            }
            
            BindingContext = this;
        }

        // Setup properties for data binding
        public string DataSource { get; set; }
        public string InitialCatalog { get; set; }
        public bool IntegratedSecurity { get; set; } = true;
        public string UserID { get; set; }
        public string Password { get; set; }
        public string ConnectionString { get; set; }
        public bool Encrypt { get; set; } = true;  // Default to true for better security
        public bool TrustServerCertificate { get; set; } = false;

        // New property to store configuration issues
        public ObservableCollection<ConfigurationIssue> ConfigurationIssues { get; private set; } = new ObservableCollection<ConfigurationIssue>();

        // Class to represent a SQL Server configuration issue
        public class ConfigurationIssue
        {
            public string Name { get; set; }
            public string CurrentValue { get; set; }
            public string RecommendedValue { get; set; }
            public string Impact { get; set; }
            public string Resolution { get; set; }
        }

        private void OnIntegratedSecurityToggled(object sender, ToggledEventArgs e)
        {
            // Disable UserId and Password if Integrated Security is enabled
            bool useIntegratedSecurity = e.Value;
            UserIdEntry.IsEnabled = !useIntegratedSecurity;
            PasswordEntry.IsEnabled = !useIntegratedSecurity;
        }

        private void OnBuildConnectionStringClicked(object sender, EventArgs e)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = DataSourceEntry.Text,
                    InitialCatalog = InitialCatalogEntry.Text,
                    IntegratedSecurity = IntegratedSecuritySwitch.IsToggled,
                    Encrypt = EncryptCheckBox.IsChecked,
                    TrustServerCertificate = TrustServerCertificateCheckBox.IsChecked
                };

                if (!builder.IntegratedSecurity)
                {
                    builder.UserID = UserIdEntry.Text;
                    builder.Password = PasswordEntry.Text;
                }

                ConnectionString = builder.ConnectionString;
                ResultLabel.Text = ConnectionString;
            }
            catch (Exception ex)
            {
                ResultLabel.Text = $"Error: {ex.Message}";
            }
        }

        // Enhanced test connection that also checks for misconfigurations
        private async void OnTestConnectionClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                await DisplayAlert("Error", "Please build a connection string first", "OK");
                return;
            }

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();
                    await DisplayAlert("Success", "Connection successful!", "OK");
                    
                    // Check for common misconfigurations
                    await CheckForMisconfigurations(connection);
                    
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Connection Failed", $"Error: {ex.Message}", "OK");
            }
        }

        // Method to check for common SQL Server misconfigurations
        private async Task CheckForMisconfigurations(SqlConnection connection)
        {
            ConfigurationIssues.Clear();
            
            await CheckAutoShrink(connection);
            await CheckAutoClose(connection);
            await CheckMaxDOP(connection);
            await CheckCostThresholdForParallelism(connection);
            await CheckCompatibilityLevel(connection);
            await CheckRecoveryModel(connection);
            await CheckBackupStatus(connection);
            await CheckPageVerification(connection);
            await CheckFileGrowthSettings(connection);
            
            // If you added a ListView or CollectionView to show these issues,
            // you would refresh it here
        }

        private async Task CheckAutoShrink(SqlConnection connection)
        {
            try
            {
                string query = @"
                    SELECT name, is_auto_shrink_on 
                    FROM sys.databases 
                    WHERE is_auto_shrink_on = 1";
                
                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string dbName = reader["name"].ToString();
                        ConfigurationIssues.Add(new ConfigurationIssue
                        {
                            Name = $"Auto Shrink Enabled for {dbName}",
                            CurrentValue = "Enabled",
                            RecommendedValue = "Disabled",
                            Impact = "Performance degradation, file fragmentation, and excessive I/O operations",
                            Resolution = $"ALTER DATABASE [{dbName}] SET AUTO_SHRINK OFF"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking auto shrink: {ex.Message}");
            }
        }

        private async Task CheckAutoClose(SqlConnection connection)
        {
            try
            {
                string query = @"
                    SELECT name, is_auto_close_on 
                    FROM sys.databases 
                    WHERE is_auto_close_on = 1";
                
                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string dbName = reader["name"].ToString();
                        ConfigurationIssues.Add(new ConfigurationIssue
                        {
                            Name = $"Auto Close Enabled for {dbName}",
                            CurrentValue = "Enabled",
                            RecommendedValue = "Disabled",
                            Impact = "Performance degradation due to connection overhead",
                            Resolution = $"ALTER DATABASE [{dbName}] SET AUTO_CLOSE OFF"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking auto close: {ex.Message}");
            }
        }

        private async Task CheckMaxDOP(SqlConnection connection)
        {
            try
            {
                string query = @"
                    SELECT value_in_use 
                    FROM sys.configurations 
                    WHERE name = 'max degree of parallelism'";
                
                using (var command = new SqlCommand(query, connection))
                {
                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        int maxDop = Convert.ToInt32(result);
                        if (maxDop == 0 || maxDop > 8)
                        {
                            ConfigurationIssues.Add(new ConfigurationIssue
                            {
                                Name = "Max Degree of Parallelism",
                                CurrentValue = maxDop.ToString(),
                                RecommendedValue = "Between 1 and 8, based on CPU cores",
                                Impact = "Query performance issues, excessive CPU usage",
                                Resolution = "EXEC sp_configure 'max degree of parallelism', <appropriate value>; RECONFIGURE;"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking Max DOP: {ex.Message}");
            }
        }

        private async Task CheckCostThresholdForParallelism(SqlConnection connection)
        {
            try
            {
                string query = @"
                    SELECT value_in_use 
                    FROM sys.configurations 
                    WHERE name = 'cost threshold for parallelism'";
                
                using (var command = new SqlCommand(query, connection))
                {
                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        int threshold = Convert.ToInt32(result);
                        if (threshold < 25)  // Default is 5, which is often too low
                        {
                            ConfigurationIssues.Add(new ConfigurationIssue
                            {
                                Name = "Cost Threshold for Parallelism",
                                CurrentValue = threshold.ToString(),
                                RecommendedValue = "25-50",
                                Impact = "Small queries using parallelism unnecessarily",
                                Resolution = "EXEC sp_configure 'cost threshold for parallelism', 25; RECONFIGURE;"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking Cost Threshold: {ex.Message}");
            }
        }

        private async Task CheckCompatibilityLevel(SqlConnection connection)
        {
            try
            {
                string query = @"
                    SELECT name, compatibility_level, 
                    (SELECT SERVERPROPERTY('ProductMajorVersion')) as server_version
                    FROM sys.databases
                    WHERE database_id > 4";  // Skip system databases
                
                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int compatLevel = Convert.ToInt32(reader["compatibility_level"]);
                        int serverVersion = Convert.ToInt32(reader["server_version"]);
                        string dbName = reader["name"].ToString();
                        
                        if (compatLevel < serverVersion * 10)
                        {
                            ConfigurationIssues.Add(new ConfigurationIssue
                            {
                                Name = $"Outdated Compatibility Level for {dbName}",
                                CurrentValue = compatLevel.ToString(),
                                RecommendedValue = (serverVersion * 10).ToString(),
                                Impact = "Missing performance improvements and features",
                                Resolution = $"ALTER DATABASE [{dbName}] SET COMPATIBILITY_LEVEL = {serverVersion * 10}"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking compatibility level: {ex.Message}");
            }
        }

        private async Task CheckRecoveryModel(SqlConnection connection)
        {
            try
            {
                string query = @"
                    SELECT name, recovery_model_desc
                    FROM sys.databases
                    WHERE database_id > 4 AND recovery_model_desc = 'SIMPLE'";
                
                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string dbName = reader["name"].ToString();
                        ConfigurationIssues.Add(new ConfigurationIssue
                        {
                            Name = $"Simple Recovery Model for {dbName}",
                            CurrentValue = "SIMPLE",
                            RecommendedValue = "FULL (for production databases)",
                            Impact = "No point-in-time recovery, only full backups usable",
                            Resolution = $"ALTER DATABASE [{dbName}] SET RECOVERY FULL"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking recovery model: {ex.Message}");
            }
        }

        private async Task CheckBackupStatus(SqlConnection connection)
        {
            try
            {
                string query = @"
                    SELECT d.name, MAX(b.backup_finish_date) as last_backup_date
                    FROM sys.databases d
                    LEFT JOIN msdb.dbo.backupset b ON d.name = b.database_name
                    WHERE d.database_id > 4
                    GROUP BY d.name";
                
                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string dbName = reader["name"].ToString();
                        if (reader["last_backup_date"] == DBNull.Value)
                        {
                            ConfigurationIssues.Add(new ConfigurationIssue
                            {
                                Name = $"No Backups for {dbName}",
                                CurrentValue = "Never",
                                RecommendedValue = "Regular backup schedule",
                                Impact = "Data loss risk",
                                Resolution = "Implement regular backup strategy"
                            });
                        }
                        else
                        {
                            DateTime lastBackup = (DateTime)reader["last_backup_date"];
                            if (lastBackup < DateTime.Now.AddDays(-7))
                            {
                                ConfigurationIssues.Add(new ConfigurationIssue
                                {
                                    Name = $"Old Backup for {dbName}",
                                    CurrentValue = lastBackup.ToString("yyyy-MM-dd"),
                                    RecommendedValue = "Within last 24 hours",
                                    Impact = "Potential data loss",
                                    Resolution = "Update backup schedule"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking backup status: {ex.Message}");
            }
        }

        private async Task CheckPageVerification(SqlConnection connection)
        {
            try
            {
                string query = @"
                    SELECT name, page_verify_option_desc
                    FROM sys.databases
                    WHERE page_verify_option_desc != 'CHECKSUM'";
                
                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string dbName = reader["name"].ToString();
                        string verifyOption = reader["page_verify_option_desc"].ToString();
                        ConfigurationIssues.Add(new ConfigurationIssue
                        {
                            Name = $"Suboptimal Page Verification for {dbName}",
                            CurrentValue = verifyOption,
                            RecommendedValue = "CHECKSUM",
                            Impact = "Reduced ability to detect corruption",
                            Resolution = $"ALTER DATABASE [{dbName}] SET PAGE_VERIFY CHECKSUM"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking page verification: {ex.Message}");
            }
        }

        private async Task CheckFileGrowthSettings(SqlConnection connection)
        {
            try
            {
                string query = @"
                    SELECT DB_NAME(database_id) as database_name, 
                           name as file_name, 
                           type_desc,
                           is_percent_growth,
                           growth
                    FROM sys.master_files
                    WHERE is_percent_growth = 1 OR growth = 1024";
                
                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        bool isPercentGrowth = Convert.ToBoolean(reader["is_percent_growth"]);
                        int growth = Convert.ToInt32(reader["growth"]);
                        string fileType = reader["type_desc"].ToString();
                        string dbName = reader["database_name"].ToString();
                        string fileName = reader["file_name"].ToString();
                        
                        if (isPercentGrowth)
                        {
                            ConfigurationIssues.Add(new ConfigurationIssue
                            {
                                Name = $"Percent-based File Growth for {dbName} ({fileName})",
                                CurrentValue = $"{growth}%",
                                RecommendedValue = "Fixed size (8MB-1GB based on database size)",
                                Impact = "Unpredictable growth and potential for excessive file size increases",
                                Resolution = $"Use ALTER DATABASE to set fixed growth increment"
                            });
                        }
                        else if (growth <= 1024) // 1MB or less
                        {
                            ConfigurationIssues.Add(new ConfigurationIssue
                            {
                                Name = $"Small Growth Increment for {dbName} ({fileName})",
                                CurrentValue = $"{growth/1024} MB",
                                RecommendedValue = "8MB-1GB based on database size",
                                Impact = "Frequent small growth events causing performance issues",
                                Resolution = $"Use ALTER DATABASE to increase growth increment"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking file growth settings: {ex.Message}");
            }
        }

        private async void OnSaveConnectionClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                await DisplayAlert("Error", "Please build a connection string first", "OK");
                return;
            }

            // Save to secure storage
            await SecureStorage.SetAsync("SqlConnectionString", ConnectionString);
            await DisplayAlert("Success", "Connection saved successfully", "OK");
            
            // Return to the dashboard with the new connection string
            await Navigation.PopAsync();
        }

        private async void OnOpenDashboardClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                await DisplayAlert("Error", "Please build and test a connection string first", "OK");
                return;
            }
            
            // Rather than pushing a new instance, go back to the previous page
            // and refresh the dashboard with the new connection
            await SecureStorage.SetAsync("SqlConnectionString", ConnectionString);
            await Navigation.PopAsync();
        }
    }
}