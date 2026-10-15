using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using M800.App.Platform;
using M800.Core.Model;
using M800.Core.Services;

namespace M800.App.ViewModels;

public sealed partial class TestsViewModel : ObservableObject
{
    private const double PollingSeconds = 3;
    private const int TargetClicks = 20;
    private const double WaitForMovementSeconds = 10;

    private enum Mode { None, Polling, Clicks }

    private readonly AppHost _host;
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly List<double> _pollTimes = [];
    private readonly List<ClickEvent> _clicks = [];
    private RawMouseInput? _input;
    private Mode _mode;
    private double _pollStartedAt = -1;
    private double _pollRequestedAt;
    private int _clickCount;

    [ObservableProperty] private string _pollingValue = "—";
    [ObservableProperty] private string _pollingDetail = "Clique em Iniciar e mexa o M800 em círculos rápidos sobre a área por 3 segundos.";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartPollingCommand), nameof(StartClicksCommand))]
    private bool _pollingRunning;

    [ObservableProperty] private string _clickValue = "—";
    [ObservableProperty] private string _clickDetail = "Clique em Iniciar e depois dê 20 cliques normais com o botão esquerdo dentro da área.";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartPollingCommand), nameof(StartClicksCommand), nameof(FinishClicksCommand))]
    private bool _clickRunning;

    [ObservableProperty] private string _suggestionText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplySuggestion))]
    private int? _suggestedDebounce;

    public TestsViewModel(AppHost host)
    {
        _host = host;
        _pollTimer.Tick += (_, _) => OnPollTick();
    }

    public bool CanApplySuggestion => SuggestedDebounce is int s && s != _host.Active.DebounceMs;

    private bool Idle() => !PollingRunning && !ClickRunning;

    private static double Now() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    private bool EnsureInput()
    {
        if (_input is not null)
            return true;
        try
        {
            _input = new RawMouseInput(Application.Current.MainWindow);
            _input.Report += OnReport;
            return true;
        }
        catch (Win32Exception ex)
        {
            PollingDetail = ClickDetail = "Não foi possível ler o mouse: " + ex.Message;
            return false;
        }
    }

    /// <summary>Encerra qualquer teste em andamento e para de ler o mouse (ao sair da página).</summary>
    public void Stop()
    {
        _mode = Mode.None;
        _pollTimer.Stop();
        PollingRunning = false;
        ClickRunning = false;
        _input?.Dispose();
        _input = null;
    }

    [RelayCommand(CanExecute = nameof(Idle))]
    private void StartPolling()
    {
        if (!EnsureInput())
            return;
        _pollTimes.Clear();
        _pollStartedAt = -1;
        _pollRequestedAt = Now();
        _mode = Mode.Polling;
        PollingRunning = true;
        PollingValue = "…";
        PollingDetail = "Mexa o M800 em círculos rápidos sobre a área…";
        _pollTimer.Start();
    }

    [RelayCommand(CanExecute = nameof(Idle))]
    private void StartClicks()
    {
        if (!EnsureInput())
            return;
        _clicks.Clear();
        _clickCount = 0;
        _mode = Mode.Clicks;
        ClickRunning = true;
        ClickValue = $"0 / {TargetClicks}";
        ClickDetail = "Dê cliques normais (um de cada vez) com o botão esquerdo do M800 dentro da área.";
        SuggestedDebounce = null;
        SuggestionText = "";
    }

    [RelayCommand(CanExecute = nameof(ClickRunning))]
    private void FinishClicks()
    {
        _mode = Mode.None;
        ClickRunning = false;
        int debounce = _host.Active.DebounceMs;
        var r = ClickAnalyzer.Analyze(_clicks, debounce);
        ClickValue = $"{r.Clicks} cliques";
        ClickDetail = r.Clicks == 0
            ? "Nenhum clique do M800 recebido."
            : $"Aperto médio {r.AveragePressMs:N0} ms · menor intervalo entre cliques {r.ShortestIntervalMs:N0} ms · debounce atual {debounce} ms";
        SuggestedDebounce = r.SuggestedDebounceMs;
        SuggestionText = r.SuggestedDebounceMs switch
        {
            null => $"Faça pelo menos {ClickAnalyzer.MinClicks} cliques para receber uma sugestão.",
            int s when r.SuspectedChatter > 0 =>
                $"Possível repique em {r.SuspectedChatter} clique(s) (dois cliques em menos de {ClickAnalyzer.ChatterIntervalMs:N0} ms). Sugestão: aumentar o debounce para {s} ms.",
            int s when s == debounce => "Nenhum repique. O debounce já está no mínimo seguro.",
            int s => $"Nenhum repique em {r.Clicks} cliques. Dá para testar {s} ms (clique mais rápido) e repetir o teste.",
        };
    }

    [RelayCommand]
    private void ApplySuggestion()
    {
        if (SuggestedDebounce is not int value)
            return;
        _host.Update(Section.Debounce, p => p.DebounceMs = value);
        OnPropertyChanged(nameof(CanApplySuggestion));
        SuggestionText = $"Debounce alterado para {value} ms. Repita o teste para conferir.";
    }

    private void OnReport(RawMouseReport report)
    {
        if (_mode == Mode.None || _input is null || !_input.IsM800(report.Device))
            return;
        if (_mode == Mode.Polling)
        {
            if (report.DeltaX == 0 && report.DeltaY == 0)
                return;
            if (_pollStartedAt < 0)
                _pollStartedAt = report.TimeMs;
            _pollTimes.Add(report.TimeMs);
            return;
        }
        if ((report.ButtonFlags & RawMouseInput.LeftButtonDown) != 0)
        {
            _clicks.Add(new ClickEvent(report.TimeMs, true));
            _clickCount++;
            ClickValue = $"{_clickCount} / {TargetClicks}";
        }
        if ((report.ButtonFlags & RawMouseInput.LeftButtonUp) != 0)
        {
            _clicks.Add(new ClickEvent(report.TimeMs, false));
            if (_clickCount >= TargetClicks)
                FinishClicks();
        }
    }

    private void OnPollTick()
    {
        if (_pollStartedAt < 0)
        {
            if ((Now() - _pollRequestedAt) / 1000.0 < WaitForMovementSeconds)
                return;
            _pollTimer.Stop();
            _mode = Mode.None;
            PollingRunning = false;
            PollingValue = "—";
            PollingDetail = "Nenhum movimento do M800 recebido. Ligue o mouse e mexa-o logo depois de clicar em Iniciar.";
            return;
        }
        double elapsed = (Now() - _pollStartedAt) / 1000.0;
        PollingDetail = $"Medindo… {Math.Min(elapsed, PollingSeconds):N1} s";
        if (elapsed >= PollingSeconds)
            FinishPolling();
    }

    private void FinishPolling()
    {
        _pollTimer.Stop();
        _mode = Mode.None;
        PollingRunning = false;
        var r = PollingAnalyzer.Analyze(_pollTimes);
        if (r is null)
        {
            PollingValue = "—";
            PollingDetail = "Poucos dados. Mexa o mouse mais rápido e sem parar durante o teste.";
            return;
        }
        int configured = _host.Active.PollingRateHz;
        PollingValue = $"{r.AverageHz:N0} Hz";
        string verdict = r.AverageHz >= configured * 0.9
            ? "dentro do esperado"
            : "abaixo do configurado: mexa mais rápido, aproxime o receptor ou use o cabo";
        PollingDetail = $"Configurado: {configured} Hz · pico {r.PeakHz:N0} Hz · estabilidade {r.StabilityPercent:N0}% · {verdict}";
    }
}
