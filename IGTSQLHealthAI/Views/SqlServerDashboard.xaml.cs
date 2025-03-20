using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using IGTSQLHealthAI.ViewModels;
using IGTSQLHealthAI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IGTSQLHealthAI.Views
{
    public partial class SqlServerDashboard : ContentPage
    {
        private readonly SqlServerDashboardViewModel _viewModel;

        public SqlServerDashboard(SqlServerDashboardViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Don't automatically test connections, just show the connection status
            _viewModel.UpdateConnectionDisplay();
            
            // Only refresh data if we already have a successful connection and no data
            if (_viewModel.ConnectionSuccessful && _viewModel.Databases.Count == 0 && !_viewModel.IsLoading)
            {
                _viewModel.RefreshCommand.ExecuteAsync(null);
            }
        }
    }
}
