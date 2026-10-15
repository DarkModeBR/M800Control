using System.Windows.Controls;
using M800.App.ViewModels;

namespace M800.App.Pages;

public partial class ProfilesPage : Page
{
    public ProfilesPage(ProfilesViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => viewModel.Load();
    }
}
