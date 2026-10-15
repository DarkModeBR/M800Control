using System.Windows.Controls;
using M800.App.ViewModels;

namespace M800.App.Pages;

public partial class PerformancePage : Page
{
    public PerformancePage(PerformanceViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => viewModel.Load();
    }
}
