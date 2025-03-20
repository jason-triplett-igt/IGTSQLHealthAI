using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IGTSQLHealthAI.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace IGTSQLHealthAI.ViewModels
{
    public class ConnectionStringViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private string _server;
        private string _database;
        private bool _useIntegratedSecurity = true;
        private string _username;
        private string _password;
        private string _resultMessage;
        private bool _isBusy;
        private bool _isSuccess;

        public ConnectionStringViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            SaveCommand = new RelayCommand(SaveConnectionString, CanSaveConnectionString);
            TestCommand = new AsyncRelayCommand(TestConnectionAsync);
            
            // Parse current connection string if available
            var sqlHelper = _serviceProvider.GetService<ISqlServerHelper>();
            if (sqlHelper != null)
            {
                ParseConnectionString(sqlHelper.ConnectionString);
            }
        }

        public string Server
        {
            get => _server;
            set => SetProperty(ref _server, value);
        }

        public string Database
        {
            get => _database;
            set => SetProperty(ref _database, value);
        }

        public bool UseIntegratedSecurity
        {
            get => _useIntegratedSecurity;
            set => SetProperty(ref _useIntegratedSecurity, value);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ResultMessage
        {
            get => _resultMessage;
            set => SetProperty(ref _resultMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool IsSuccess
        {
            get => _isSuccess;
            set => SetProperty(ref _isSuccess, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand TestCommand { get; }

        private bool CanSaveConnectionString()
        {
            return !string.IsNullOrWhiteSpace(Server);
        }

        private void SaveConnectionString()
        {
            try
            {
                var connectionString = BuildConnectionString();
                
                // Update the connection manager with the new connection string
                var connectionManager = _serviceProvider.GetRequiredService<ConnectionManager>();
                connectionManager.UpdateConnectionString(connectionString);
                
                ResultMessage = $"Connection settings saved for {Server}.";
                IsSuccess = true;
                
                // Go back to the dashboard
                Application.Current.MainPage.Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                ResultMessage = $"Error saving connection string: {ex.Message}";
                IsSuccess = false;
            }
        }

        private async Task TestConnectionAsync()
        {
            IsBusy = true;
            ResultMessage = $"Testing connection to {Server}...";
            IsSuccess = false;

            try
            {
                var connectionString = BuildConnectionString();
                var tempHelper = new SqlServerHelper(connectionString);
                var result = await tempHelper.TestConnectionAsync();

                if (result.Success)
                {
                    ResultMessage = $"Successfully connected to {Server}!";
                    IsSuccess = true;
                }
                else
                {
                    ResultMessage = $"Failed to connect to {Server}: {result.ErrorMessage}";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                ResultMessage = $"Connection error to {Server}: {ex.Message}";
                IsSuccess = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string BuildConnectionString()
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
            {
                DataSource = Server,
                InitialCatalog = Database
            };

            if (UseIntegratedSecurity)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = Username;
                builder.Password = Password;
            }

            // Add common options
            builder.TrustServerCertificate = true;
            builder.MultipleActiveResultSets = true;

            return builder.ConnectionString;
        }

        private void ParseConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return;

            try
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
                
                Server = builder.DataSource;
                Database = builder.InitialCatalog;
                UseIntegratedSecurity = builder.IntegratedSecurity;
                
                if (!UseIntegratedSecurity)
                {
                    Username = builder.UserID;
                    // We don't populate the password for security reasons
                }
            }
            catch
            {
                // If we can't parse it, just leave the default values
            }
        }
    }
}
