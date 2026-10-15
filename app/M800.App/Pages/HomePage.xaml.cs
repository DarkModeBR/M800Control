using System.Windows.Controls;
using M800.App.ViewModels;

namespace M800.App.Pages;

public partial class HomePage : Page
{
    public HomePage(HomeViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => viewModel.Refresh();
    }
}
