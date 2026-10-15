using System.Windows;
using System.Windows.Threading;
using M800.App.Common;
using M800.App.Platform;

namespace M800.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private AppHost? _host;
    private Tray.TrayIcon? _tray;

    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "M800Control");

    protected override void OnStartup(StartupEventArgs e)
    {
        _instance = SingleInstance.TryAcquire();
        if (_instance is null)
        {
            SingleInstance.SignalExisting();
            Shutdown();
            return;
        }
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        Responsive.EnableAdaptiveCards();
        _host = AppHost.Create(DataDirectory, Dispatcher);
        StartupRegistration.Sync(_host.Settings.StartWithWindows);
        var window = new MainWindow(_host);
        MainWindow = window;
        _tray = new Tray.TrayIcon(_host, window);
        _ = new Controls.DpiIndicator(_host);
        ThemeService.Apply(_host.Settings.Theme, window);
        _instance.ShowRequested += () => Dispatcher.BeginInvoke(window.ShowFromTray);

        if (e.Args.Contains("--minimized"))
            window.StartHidden();
        else
            window.Show();
        _host.Start();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.AppendAllText(Path.Combine(DataDirectory, "erros.txt"), $"{DateTime.Now:u} {e.Exception}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        MessageBox.Show(e.Exception.Message, "M800 Control — erro inesperado", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _host?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
