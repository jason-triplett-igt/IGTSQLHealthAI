using Microsoft.Maui.Controls;
using IGTSQLHealthAI.Views;
using IGTSQLHealthAI.ViewModels;
using IGTSQLHealthAI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IGTSQLHealthAI
{
    public partial class App : Application
    {
        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            // Get the view model from the service provider
            var viewModel = serviceProvider.GetRequiredService<SqlServerDashboardViewModel>();
            
            // Create the main page with the view model
            MainPage = new NavigationPage(new SqlServerDashboard(viewModel));
        }
    }
}