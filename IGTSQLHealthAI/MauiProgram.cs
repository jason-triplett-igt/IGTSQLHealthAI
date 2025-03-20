using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls.Hosting;
using IGTSQLHealthAI.Services;
using IGTSQLHealthAI.Services.Data;
using IGTSQLHealthAI.ViewModels;
using IGTSQLHealthAI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace IGTSQLHealthAI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Default connection string - will be configurable by user
            string defaultConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

            // Register ConnectionManager as singleton to maintain connection string state
            builder.Services.AddSingleton<ConnectionManager>(sp => 
                new ConnectionManager(sp, defaultConnectionString, sp.GetService<ILogger<ConnectionManager>>()));
            
            // Register data services
            builder.Services.AddTransient<IDatabaseService, DatabaseService>();
            builder.Services.AddTransient<IPerformanceService, PerformanceService>();
            builder.Services.AddTransient<ISuperPerfService, SuperPerfService>();
            
            // Make sure SqlServerHelper is always created fresh with the current connection string
            builder.Services.AddTransient<ISqlServerHelper>(sp => {
                var connectionManager = sp.GetRequiredService<ConnectionManager>();
                return connectionManager.CreateSqlServerHelper();
            });

            // Register ViewModels
            builder.Services.AddTransient<SqlServerDashboardViewModel>();
            builder.Services.AddTransient<ConnectionStringViewModel>();

            // Register Views
            builder.Services.AddTransient<SqlServerDashboard>();
            builder.Services.AddTransient<ConnectionStringPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
