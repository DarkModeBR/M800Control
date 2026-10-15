using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using M800.Core.Protocol;
using M800.Core.Services;
using Forms = System.Windows.Forms;

namespace M800.App.Tray;

/// <summary>Ícone da bandeja com a bateria, menu de perfis e notificações.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly AppHost _host;
    private readonly MainWindow _window;
    private readonly Forms.NotifyIcon _notifyIcon = new();
    private System.Drawing.Icon? _icon;
    private bool _hiddenTipShown;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    public TrayIcon(AppHost host, MainWindow window)
    {
        _host = host;
        _window = window;
        _notifyIcon.MouseClick += OnMouseClick;
        _notifyIcon.BalloonTipClicked += (_, _) => _window.ShowFromTray();
        host.StatusChanged += Refresh;
        host.BatteryLow += info => _notifyIcon.ShowBalloonTip(10000, "Bateria do mouse baixa",
            $"O M800 PRO está com {info.Battery}% de bateria. Conecte o cabo para carregar.", Forms.ToolTipIcon.Warning);
        window.IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false && !_window.ExitRequested && !_hiddenTipShown)
            {
                _hiddenTipShown = true;
                _notifyIcon.ShowBalloonTip(5000, "O M800 Control continua aberto",
                    "Ele fica aqui na bandeja cuidando do mouse. Clique no ícone para abrir.", Forms.ToolTipIcon.Info);
            }
        };
        Refresh();
        _notifyIcon.Visible = true;
    }

    private void Refresh()
    {
        var info = _host.Mouse.Info;
        var state = _host.Mouse.State;
        string text = state switch
        {
            ConnectionState.Disconnected => "M800 PRO: não conectado",
            ConnectionState.Sleeping => $"M800 PRO: dormindo · {info?.Battery}%",
            _ => $"M800 PRO: {info?.Battery}% · " + info?.Charge switch
            {
                ChargeState.Charging => "carregando",
                ChargeState.Full => "carregado",
                _ => "na bateria",
            },
        };
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
        var icon = BatteryIconRenderer.Render(state == ConnectionState.Disconnected ? null : info?.Battery,
            info?.Charge == ChargeState.Charging, _host.Settings.BatteryAlertThreshold);
        _notifyIcon.Icon = icon;
        _icon?.Dispose();
        _icon = icon;
    }

    private void OnMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
            _window.ShowFromTray();
        else if (e.Button == Forms.MouseButtons.Right)
            ShowMenu();
    }

    private void ShowMenu()
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Abrir M800 Control", FontWeight = FontWeights.SemiBold };
        open.Click += (_, _) => _window.ShowFromTray();
        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        foreach (var profile in _host.Store.Profiles)
        {
            var id = profile.Id;
            var item = new MenuItem { Header = profile.Name, IsCheckable = true, IsChecked = id == _host.Store.ActiveProfileId };
            item.Click += (_, _) => _host.ActivateProfile(id);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var exit = new MenuItem { Header = "Sair" };
        exit.Click += (_, _) =>
        {
            _window.ExitRequested = true;
            Application.Current.Shutdown();
        };
        menu.Items.Add(exit);

        SetForegroundWindow(new WindowInteropHelper(_window).EnsureHandle());
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
    }
}
