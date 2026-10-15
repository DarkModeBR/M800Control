using System.Windows.Controls;
using M800.App.ViewModels;

namespace M800.App.Pages;

public partial class LightingPage : Page
{
    public LightingPage(LightingViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => viewModel.Load();
    }
}
