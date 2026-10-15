using System.Windows.Controls;
using M800.App.ViewModels;

namespace M800.App.Pages;

public partial class DpiPage : Page
{
    public DpiPage(DpiViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => viewModel.Load();
    }
}
