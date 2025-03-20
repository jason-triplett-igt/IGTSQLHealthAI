using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Data.SqlClient;
using IGTSQLHealthAI.Services;
using IGTSQLHealthAI.Services.Data;
using IGTSQLHealthAI.Models;
using System.Collections.Generic;
using System.Windows.Input;
using IGTSQLHealthAI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IGTSQLHealthAI.ViewModels
{
    public class SqlServerDashboardViewModel : ObservableObject
    {
        private readonly ISqlServerHelper _sqlServerHelper;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConnectionManager _connectionManager;
        private readonly IDatabaseService _databaseService;
        private readonly IPerformanceService _performanceService;
        private readonly ISuperPerfService _superPerfService;
        private readonly ILogger<SqlServerDashboardViewModel> _logger;
        
        private bool _isLoading;
        private string _serverStatus;
        private ObservableCollection<DatabaseInfo> _databases = new();
        private string _errorMessage;
        private bool _connectionSuccessful;
        private bool _isTestingConnection;
        private string _serverName;
        private ObservableCollection<PerformanceMetric> _generalMetrics = new();
        private ObservableCollection<PerformanceMetric> _diskMetrics = new();
        private ObservableCollection<ProcedureMetric> _topProcedures = new();
        private ObservableCollection<InstanceData> _instanceData = new();
        private ObservableCollection<TopSP> _topSPs = new();
        private ObservableCollection<StatsFrag> _statsFrags = new();
        private ObservableCollection<IndexUsage> _indexUsages = new();
        private ObservableCollection<MissingIndex> _missingIndexes = new();
        private ObservableCollection<AvgIO> _avgIOs = new();
        private ObservableCollection<SQLWait> _sqlWaits = new();

        public SqlServerDashboardViewModel(
            ISqlServerHelper sqlServerHelper, 
            IServiceProvider serviceProvider,
            IDatabaseService databaseService,
            IPerformanceService performanceService,
            ISuperPerfService superPerfService,
            ILogger<SqlServerDashboardViewModel> logger = null)
        {
            _sqlServerHelper = sqlServerHelper;
            _serviceProvider = serviceProvider;
            _connectionManager = serviceProvider.GetRequiredService<ConnectionManager>();
            _databaseService = databaseService;
            _performanceService = performanceService;
            _superPerfService = superPerfService;
            _logger = logger;
            
            // Create commands
            RefreshCommand = new AsyncRelayCommand(LoadDashboardDataAsync);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
            NavigateToConnectionSettingsCommand = new RelayCommand(NavigateToConnectionSettings);
            
            // New Command
            ExecuteSuperPerfCommand = new AsyncRelayCommand(ExecuteSuperPerfDataAsync);
            
            // Default state
            _connectionSuccessful = false;
            
            // Listen for connection string updates
            _connectionManager.ConnectionStringUpdated += OnConnectionStringUpdated;
        }

        #region Properties

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsTestingConnection
        {
            get => _isTestingConnection;
            set => SetProperty(ref _isTestingConnection, value);
        }

        public string ServerStatus
        {
            get => _serverStatus;
            set => SetProperty(ref _serverStatus, value);
        }

        public ObservableCollection<DatabaseInfo> Databases
        {
            get => _databases;
            set => SetProperty(ref _databases, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool ConnectionSuccessful
        {
            get => _connectionSuccessful;
            set => SetProperty(ref _connectionSuccessful, value);
        }

        public string ConnectionString => _connectionManager.ConnectionString;

        public string ServerName
        {
            get => _serverName;
            private set => SetProperty(ref _serverName, value);
        }

        public ObservableCollection<PerformanceMetric> GeneralMetrics
        {
            get => _generalMetrics;
            set => SetProperty(ref _generalMetrics, value);
        }

        public ObservableCollection<PerformanceMetric> DiskMetrics
        {
            get => _diskMetrics;
            set => SetProperty(ref _diskMetrics, value);
        }

        public ObservableCollection<ProcedureMetric> TopProcedures
        {
            get => _topProcedures;
            set => SetProperty(ref _topProcedures, value);
        }

        public ObservableCollection<InstanceData> InstanceData
        {
            get => _instanceData;
            set => SetProperty(ref _instanceData, value);
        }

        public ObservableCollection<TopSP> TopSPs
        {
            get => _topSPs;
            set => SetProperty(ref _topSPs, value);
        }

        public ObservableCollection<StatsFrag> StatsFrags
        {
            get => _statsFrags;
            set => SetProperty(ref _statsFrags, value);
        }

        public ObservableCollection<IndexUsage> IndexUsages
        {
            get => _indexUsages;
            set => SetProperty(ref _indexUsages, value);
        }

        public ObservableCollection<MissingIndex> MissingIndexes
        {
            get => _missingIndexes;
            set => SetProperty(ref _missingIndexes, value);
        }

        public ObservableCollection<AvgIO> AvgIOs
        {
            get => _avgIOs;
            set => SetProperty(ref _avgIOs, value);
        }

        public ObservableCollection<SQLWait> SQLWaits
        {
            get => _sqlWaits;
            set => SetProperty(ref _sqlWaits, value);
        }

        #endregion

        #region Commands

        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand TestConnectionCommand { get; }
        public ICommand NavigateToConnectionSettingsCommand { get; }

        // New Command
        public IAsyncRelayCommand ExecuteSuperPerfCommand { get; }

        #endregion

        #region Public Methods

        public void UpdateConnectionDisplay()
        {
            ServerName = ExtractServerName(_sqlServerHelper.ConnectionString);
            
            if (ConnectionSuccessful)
            {
                ServerStatus = $"Connected to {ServerName}. Ready to load data.";
            }
            else
            {
                ServerStatus = $"Not connected to {ServerName}. Please test the connection.";
            }
        }

        public async Task TestConnectionAsync()
        {
            try
            {
                // Always get a fresh helper to ensure we're using the latest connection string
                var helper = GetCurrentSqlServerHelper();
                ServerName = ExtractServerName(helper.ConnectionString);
                
                IsTestingConnection = true;
                IsLoading = true;
                ErrorMessage = string.Empty;
                ServerStatus = $"Testing connection to {ServerName}...";
                
                var result = await helper.TestConnectionAsync();
                ConnectionSuccessful = result.Success;
                
                if (ConnectionSuccessful)
                {
                    ServerStatus = $"Connected to {ServerName}! Click Refresh to load data.";
                }
                else
                {
                    ErrorMessage = $"Failed to connect to {ServerName}: {result.ErrorMessage}";
                    ServerStatus = $"Connection to {ServerName} failed";
                }
            }
            catch (Exception ex)
            {
                ConnectionSuccessful = false;
                ServerName = ExtractServerName(_sqlServerHelper.ConnectionString);
                ErrorMessage = $"Connection test error to {ServerName}: {ex.Message}";
                ServerStatus = "Connection error";
                _logger?.LogError(ex, "Error testing connection to {Server}", ServerName);
            }
            finally
            {
                IsLoading = false;
                IsTestingConnection = false;
            }
        }

        public async Task LoadDashboardDataAsync()
        {
            if (!ConnectionSuccessful)
            {
                // First try to test the connection
                await TestConnectionAsync();
                
                // If it's still not successful, don't proceed
                if (!ConnectionSuccessful)
                {
                    ErrorMessage = "Cannot load dashboard data without a successful connection. Please check your connection settings.";
                    return;
                }
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // Get a fresh SqlServerHelper that uses the current connection string
                var helper = GetCurrentSqlServerHelper();

                // Load data using the services
                await LoadServerStatusAsync(helper);
                await LoadDatabasesAsync(helper);
                await LoadPerformanceMetricsAsync(helper);
                await LoadSuperPerfDataAsync(helper);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading dashboard data: {ex.Message}";
                _logger?.LogError(ex, "Error loading dashboard data");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Private Methods

        private ISqlServerHelper GetCurrentSqlServerHelper()
        {
            // This ensures we're always using the latest connection string
            return _serviceProvider.GetRequiredService<ISqlServerHelper>();
        }

        private string ExtractServerName(string connectionString)
        {
            try
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
                return builder.DataSource;
            }
            catch
            {
                return "unknown server";
            }
        }

        private async Task LoadServerStatusAsync(ISqlServerHelper helper)
        {
            try
            {
                ServerStatus = await _databaseService.GetServerStatusAsync(helper);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading server status");
                ServerStatus = $"Connected to {ServerName}, but failed to retrieve server details.";
            }
        }

        private async Task LoadDatabasesAsync(ISqlServerHelper helper)
        {
            try
            {
                var databases = await _databaseService.GetDatabaseInfoAsync(helper);
                Databases.Clear();
                foreach (var db in databases)
                {
                    Databases.Add(db);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading databases");
                ErrorMessage = $"Failed to load database information: {ex.Message}";
            }
        }

        private async Task LoadPerformanceMetricsAsync(ISqlServerHelper helper)
        {
            try
            {
                // Load general metrics
                var generalMetrics = await _performanceService.GetGeneralMetricsAsync(helper);
                GeneralMetrics.Clear();
                foreach (var metric in generalMetrics)
                {
                    GeneralMetrics.Add(metric);
                }

                // Load disk metrics
                var diskMetrics = await _performanceService.GetDiskMetricsAsync(helper);
                DiskMetrics.Clear();
                foreach (var metric in diskMetrics)
                {
                    DiskMetrics.Add(metric);
                }

                // Load top procedures
                var procedures = await _performanceService.GetTopProceduresAsync(helper);
                TopProcedures.Clear();
                foreach (var proc in procedures)
                {
                    TopProcedures.Add(proc);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading performance metrics");
                // Don't set error message to avoid disrupting the whole dashboard
            }
        }

        private async Task LoadSuperPerfDataAsync(ISqlServerHelper helper)
        {
            try
            {
                await helper.ExecuteSuperPerfAsync();
                // Load Instance Data
                var instanceData = await _superPerfService.GetInstanceDataAsync(helper);
                InstanceData = new ObservableCollection<InstanceData>(instanceData);

                // Load TopSPs
                var topSPs = await _superPerfService.GetTopSPsAsync(helper);
                TopSPs = new ObservableCollection<TopSP>(topSPs);

                // Load StatsFrags
                var statsFrags = await _superPerfService.GetStatsFragsAsync(helper);
                StatsFrags = new ObservableCollection<StatsFrag>(statsFrags);

                // Load IndexUsages
                var indexUsages = await _superPerfService.GetIndexUsagesAsync(helper);
                IndexUsages = new ObservableCollection<IndexUsage>(indexUsages);

                // Load MissingIndexes
                var missingIndexes = await _superPerfService.GetMissingIndexesAsync(helper);
                MissingIndexes = new ObservableCollection<MissingIndex>(missingIndexes);

                // Load AvgIOs
                var avgIOs = await _superPerfService.GetAvgIOsAsync(helper);
                AvgIOs = new ObservableCollection<AvgIO>(avgIOs);

                // Load SQLWaits
                var sqlWaits = await _superPerfService.GetSQLWaitsAsync(helper);
                SQLWaits = new ObservableCollection<SQLWait>(sqlWaits);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading SuperPerf data");
                // Don't set error message to avoid disrupting the whole dashboard
            }
        }
        
        private async Task ExecuteSuperPerfDataAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // Get a fresh SqlServerHelper that uses the current connection string
                var helper = GetCurrentSqlServerHelper();

                await _superPerfService.ExecuteSuperPerfAsync(helper);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error executing SuperPerf data: {ex.Message}";
                _logger?.LogError(ex, "Error executing SuperPerf data");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void NavigateToConnectionSettings()
        {
            var connectionViewModel = _serviceProvider.GetRequiredService<ConnectionStringViewModel>();
            var connectionPage = new ConnectionStringPage(connectionViewModel);
            Application.Current.MainPage.Navigation.PushAsync(connectionPage);
        }
        
        private void OnConnectionStringUpdated(object sender, EventArgs e)
        {
            // Reset connection status
            ConnectionSuccessful = false;
            
            // Update the UI to reflect that we need to test the new connection
            ServerName = ExtractServerName(_sqlServerHelper.ConnectionString);
            ServerStatus = $"Connection settings changed to {ServerName}. Please test the connection.";
            ErrorMessage = string.Empty;
            
            // Clear existing data
            Databases.Clear();
            GeneralMetrics.Clear();
            DiskMetrics.Clear();
            TopProcedures.Clear();
            InstanceData.Clear();
            TopSPs.Clear();
            StatsFrags.Clear();
            IndexUsages.Clear();
            MissingIndexes.Clear();
            AvgIOs.Clear();
            SQLWaits.Clear();
        }

        #endregion
    }
}
