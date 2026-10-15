using System.Windows.Controls;
using M800.App.ViewModels;

namespace M800.App.Pages;

public partial class TestsPage : Page
{
    public TestsPage(TestsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Unloaded += (_, _) => viewModel.Stop();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false)
                viewModel.Stop(); // janela escondida na bandeja
        };
    }
}
