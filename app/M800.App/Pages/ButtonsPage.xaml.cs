using System.Windows.Controls;
using M800.App.ViewModels;

namespace M800.App.Pages;

public partial class ButtonsPage : Page
{
    public ButtonsPage(ButtonsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => viewModel.Load();
    }
}
