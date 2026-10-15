using System.Windows.Controls;
using System.Windows.Input;
using M800.App.ViewModels;
using M800.Core.Protocol;

namespace M800.App.Pages;

public partial class MacrosPage : Page
{
    private readonly MacrosViewModel _viewModel;

    public MacrosPage(MacrosViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => viewModel.LoadMacros();
        Unloaded += (_, _) => viewModel.StopRecording(save: false);
    }

    private static byte Mask(MouseButton button) => button switch
    {
        MouseButton.Left => MouseButtons.Left,
        MouseButton.Right => MouseButtons.Right,
        MouseButton.Middle => MouseButtons.Middle,
        MouseButton.XButton1 => MouseButtons.Back,
        MouseButton.XButton2 => MouseButtons.Forward,
        _ => 0,
    };

    private void OnCaptureMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (Mask(e.ChangedButton) is var mask and not 0)
            _viewModel.RecordMouse(mask, up: false);
    }

    private void OnCaptureMouseUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (Mask(e.ChangedButton) is var mask and not 0)
            _viewModel.RecordMouse(mask, up: true);
    }
}
