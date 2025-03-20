using IGTSQLHealthAI.ViewModels;

namespace IGTSQLHealthAI.Views
{
    public partial class ConnectionStringPage : ContentPage
    {
        public ConnectionStringPage(ConnectionStringViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
