using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IGTSQLHealthAI.Services
{
    public class ConnectionManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConnectionManager> _logger;
        private string _connectionString;

        public event EventHandler ConnectionStringUpdated;

        public ConnectionManager(IServiceProvider serviceProvider, string initialConnectionString, ILogger<ConnectionManager> logger = null)
        {
            _serviceProvider = serviceProvider;
            _connectionString = initialConnectionString;
            _logger = logger;
            _logger?.LogInformation("ConnectionManager initialized with connection string: {ConnectionString}", MaskConnectionString(initialConnectionString));
        }

        public string ConnectionString => _connectionString;

        public void UpdateConnectionString(string newConnectionString)
        {
            if (_connectionString != newConnectionString)
            {
                _logger?.LogInformation("Updating connection string from {OldConnection} to {NewConnection}", 
                    MaskConnectionString(_connectionString), MaskConnectionString(newConnectionString));
                
                _connectionString = newConnectionString;
                ConnectionStringUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public ISqlServerHelper CreateSqlServerHelper()
        {
            _logger?.LogDebug("Creating SqlServerHelper with current connection string");
            return new SqlServerHelper(_connectionString, _serviceProvider.GetService<ILogger<SqlServerHelper>>());
        }

        // Mask the connection string to avoid logging sensitive information
        private string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return connectionString;

            try
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
                var server = builder.DataSource;
                var database = builder.InitialCatalog;
                return $"Server={server};Database={database};...";
            }
            catch
            {
                return "[Invalid connection string]";
            }
        }
    }
}
