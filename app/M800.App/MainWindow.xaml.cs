using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using M800.App.Common;
using M800.App.Navigation;
using M800.App.Pages;
using M800.Core.Services;
using Wpf.Ui.Controls;

namespace M800.App;

public partial class MainWindow : FluentWindow
{
    private readonly AppHost _host;

    public MainWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();
        FitToWorkArea();
        Dialogs.Host = DialogHost;
        RootNavigation.SetPageProviderService(new PageProvider(host));
        Loaded += OnLoaded;
        Closing += OnClosing;
        SizeChanged += (_, _) => AdaptToWidth();
        SourceInitialized += (_, _) => Platform.DeviceChangeWatcher.Attach(this, host.Mouse.Poke);
        host.StatusChanged += UpdateStatus;
        host.SendFailed += message =>
        {
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
        };
        host.Progress += OnProgress;
    }

    /// <summary>true quando o usuário escolheu "Sair" (senão fechar só esconde na bandeja).</summary>
    public bool ExitRequested { get; set; }

    private bool _firstLoad = true;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_firstLoad)
            return;
        _firstLoad = false;
        RootNavigation.Navigate(typeof(HomePage));
        UpdateStatus();
        if (!_host.Settings.FirstRunDone)
        {
            _host.Settings.FirstRunDone = true;
            _host.SaveNow();
            bool send = await Dialogs.ConfirmAsync("Bem-vindo ao M800 Control",
                "O mouse não permite ler a configuração que está gravada nele agora.\n\n" +
                "Enviar o perfil \"Padrão\" para o mouse agora? Se escolher \"Depois\", nada é enviado até você mudar alguma opção " +
                "(ou usar \"Reenviar ao mouse\" na página Perfis).\n\n" +
                "Dica: feche ou desinstale o driver oficial para os dois programas não disputarem o mouse.",
                "Enviar agora", "Depois");
            if (send)
                _host.ResendActive();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (ExitRequested)
            return;
        e.Cancel = true;
        Hide();
    }

    /// <summary>Em telas pequenas (ex.: 1366×768) a janela não pode passar da área útil, senão a barra de título some.</summary>
    private void FitToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width - 24);
        Height = Math.Min(Height, area.Height - 24);
        MinWidth = Math.Min(MinWidth, Width);
        MinHeight = Math.Min(MinHeight, Height);
    }

    private const double CompactPaneBelow = 860, SmallMarginsBelow = 760;
    private bool? _compact, _smallMargins;

    /// <summary>Janela estreita: menu só com ícones e margens menores (o usuário ainda pode abrir o menu pelo ☰).</summary>
    private void AdaptToWidth()
    {
        bool compact = ActualWidth < CompactPaneBelow;
        if (compact != _compact)
        {
            _compact = compact;
            RootNavigation.IsPaneOpen = !compact;
        }
        bool small = ActualWidth < SmallMarginsBelow;
        if (small == _smallMargins)
            return;
        _smallMargins = small;
        Resources["PageMargin"] = small ? new Thickness(16, 8, 16, 24) : new Thickness(42, 8, 42, 40);
        Resources["HeaderMargin"] = small ? new Thickness(16, 10, 16, 0) : new Thickness(42, 10, 42, 0);
    }

    /// <summary>Cria a janela sem mostrá-la (início com o Windows).</summary>
    public void StartHidden() => new WindowInteropHelper(this).EnsureHandle();

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void UpdateStatus()
    {
        int pending = _host.Mouse.Pending.Count;
        string pendingText = pending switch
        {
            0 => "",
            1 => " 1 alteração pendente será enviada quando ele voltar.",
            _ => $" {pending} alterações pendentes serão enviadas quando ele voltar.",
        };
        switch (_host.Mouse.State)
        {
            case ConnectionState.Disconnected:
                ShowStatus(InfoBarSeverity.Warning, "Mouse não encontrado", "Conecte o cabo ou o receptor 2.4 GHz." + pendingText);
                break;
            case ConnectionState.Sleeping:
                ShowStatus(InfoBarSeverity.Informational, "Mouse dormindo", "Mexa o mouse para acordá-lo." + pendingText);
                break;
            default:
                StatusBar.IsOpen = false;
                break;
        }
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }

    private void OnProgress(int sent, int total)
    {
        SyncProgress.Maximum = total;
        SyncProgress.Value = sent;
        SyncProgress.Visibility = total > 3 && sent < total ? Visibility.Visible : Visibility.Collapsed;
    }
}
