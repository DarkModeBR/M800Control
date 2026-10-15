using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using M800.App.Common;
using M800.App.Controls;
using M800.App.Platform;
using M800.Core.Model;
using M800.Core.Protocol;
using M800.Core.Services;

namespace M800.App.ViewModels;

public sealed partial class MacroListItem(Guid id, string name, string summary) : ObservableObject
{
    public Guid Id { get; } = id;

    [ObservableProperty] private string _name = name;
    [ObservableProperty] private string _summary = summary;
}

public sealed partial class MacroEventItem : ObservableObject
{
    private readonly MacrosViewModel _owner;
    private readonly bool _ready;

    [ObservableProperty] private double? _delayMs;

    public MacroEventItem(MacrosViewModel owner, MacroEvent e)
    {
        _owner = owner;
        Kind = e.Kind;
        Code = e.Code;
        Release = e.Release;
        DelayMs = e.DelayMs;
        _ready = true;
    }

    public MacroEventKind Kind { get; }
    public byte Code { get; }
    public bool Release { get; }
    public string Glyph => Release ? "↑" : "↓";
    public string ActionText => Release ? "soltar" : "apertar";

    public string Description => Kind switch
    {
        MacroEventKind.Key => KeyNames.Name(Code),
        MacroEventKind.Modifier => string.Join(" + ", KeyMap.ModifierNames(Code)),
        MacroEventKind.Mouse => Code switch
        {
            MouseButtons.Left => "Clique esquerdo",
            MouseButtons.Right => "Clique direito",
            MouseButtons.Middle => "Clique do meio",
            MouseButtons.Back => "Botão voltar",
            MouseButtons.Forward => "Botão avançar",
            _ => $"Botões 0x{Code:X2}",
        },
        _ => "?",
    };

    public MacroEvent ToEvent() =>
        new(Kind, Code, Release, (int)Math.Clamp(DelayMs ?? 0, 0, MacroEvent.MaxDelayMs));

    partial void OnDelayMsChanged(double? value)
    {
        if (_ready)
            _owner.SaveEvents();
    }
}

public sealed partial class MacrosViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly DispatcherTimer _escTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private LowLevelKeyboardHook? _hook;
    private MacroRecorder? _recorder;
    private long _escDownAt = -1;
    private bool _loadingEvents;

    [ObservableProperty] private MacroListItem? _selectedMacro;
    [ObservableProperty] private MacroEventItem? _selectedEvent;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _useRealDelays = true;
    [ObservableProperty] private double? _fixedDelayMs = 20;
    [ObservableProperty] private string _eventCountText = "";

    public MacrosViewModel(AppHost host)
    {
        _host = host;
        _escTimer.Tick += (_, _) =>
        {
            _escTimer.Stop();
            if (IsRecording && _escDownAt >= 0)
                StopRecording(save: true);
        };
        host.ActiveProfileChanged += () =>
        {
            StopRecording(save: false);
            LoadMacros();
        };
        LoadMacros();
    }

    public ObservableCollection<MacroListItem> Macros { get; } = [];
    public ObservableCollection<MacroEventItem> Events { get; } = [];
    public bool HasSelection => SelectedMacro is not null;
    public string RecordingText =>
        "Gravando… digite as teclas (elas não chegam ao Windows durante a gravação). " +
        "Clique dentro desta área para gravar cliques. Segure Esc por 1 segundo para parar.";

    private MacroDefinition? Current =>
        SelectedMacro is null ? null : _host.Active.Macros.FirstOrDefault(m => m.Id == SelectedMacro.Id);

    private int DefaultDelay => (int)Math.Clamp(FixedDelayMs ?? 20, 0, MacroEvent.MaxDelayMs);

    public void LoadMacros()
    {
        var selected = SelectedMacro?.Id;
        Macros.Clear();
        foreach (var m in _host.Active.Macros)
            Macros.Add(new MacroListItem(m.Id, m.Name, Summary(m)));
        SelectedMacro = Macros.FirstOrDefault(x => x.Id == selected) ?? Macros.FirstOrDefault();
        LoadEvents();
    }

    private string Summary(MacroDefinition macro)
    {
        var users = ButtonIds.All.Where(b => _host.Active.Buttons[(int)b].MacroId == macro.Id).Select(ButtonIds.DisplayName).ToList();
        return $"{macro.Events.Count} eventos" + (users.Count > 0 ? " · " + string.Join(", ", users) : " · não usada");
    }

    partial void OnSelectedMacroChanged(MacroListItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        if (IsRecording)
            StopRecording(save: false);
        LoadEvents();
        RefreshCommands();
    }

    partial void OnSelectedEventChanged(MacroEventItem? value) => RefreshCommands();

    partial void OnIsRecordingChanged(bool value) => OnPropertyChanged(nameof(RecordingText));

    private void LoadEvents()
    {
        _loadingEvents = true;
        Events.Clear();
        if (Current is { } macro)
        {
            foreach (var e in macro.Events)
                Events.Add(new MacroEventItem(this, e));
        }
        _loadingEvents = false;
        UpdateCount();
    }

    private void UpdateCount() => EventCountText = $"{Events.Count} de {Payloads.MaxMacroEvents} eventos";

    internal void SaveEvents()
    {
        if (_loadingEvents || Current is not { } macro)
            return;
        var events = Events.Select(e => e.ToEvent()).ToList();
        var id = macro.Id;
        void Change(Profile p) => p.Macros.First(m => m.Id == id).Events = events;
        if (_host.Active.IsMacroUsed(id))
            _host.Update(Section.Buttons, Change);
        else
            _host.UpdateLocal(Change);
        UpdateCount();
        if (SelectedMacro is { } item)
            item.Summary = Summary(_host.Active.Macros.First(m => m.Id == id));
    }

    private void RefreshCommands()
    {
        RenameMacroCommand.NotifyCanExecuteChanged();
        DuplicateMacroCommand.NotifyCanExecuteChanged();
        DeleteMacroCommand.NotifyCanExecuteChanged();
        InsertKeyCommand.NotifyCanExecuteChanged();
        InsertClickCommand.NotifyCanExecuteChanged();
        ToggleRecordingCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        DeleteEventCommand.NotifyCanExecuteChanged();
    }

    private bool HasEvent() => SelectedEvent is not null && !IsRecording;

    [RelayCommand]
    private async Task NewMacroAsync()
    {
        var name = await Dialogs.PromptAsync("Nova macro", $"Macro {Macros.Count + 1}");
        if (name is null)
            return;
        var macro = new MacroDefinition { Name = name };
        _host.UpdateLocal(p => p.Macros.Add(macro));
        LoadMacros();
        SelectedMacro = Macros.First(x => x.Id == macro.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RenameMacroAsync()
    {
        var macro = Current!;
        var name = await Dialogs.PromptAsync("Renomear macro", macro.Name);
        if (name is null)
            return;
        _host.UpdateLocal(p => p.Macros.First(m => m.Id == macro.Id).Name = name);
        SelectedMacro!.Name = name;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DuplicateMacro()
    {
        var macro = Current!;
        var copy = new MacroDefinition { Name = macro.Name + " (cópia)", Events = macro.Events.ToList() };
        _host.UpdateLocal(p => p.Macros.Add(copy));
        LoadMacros();
        SelectedMacro = Macros.First(x => x.Id == copy.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteMacroAsync()
    {
        var macro = Current!;
        bool used = _host.Active.IsMacroUsed(macro.Id);
        string message = used
            ? $"A macro \"{macro.Name}\" está ligada a um botão, que voltará à função de fábrica."
            : $"Excluir a macro \"{macro.Name}\"?";
        if (!await Dialogs.ConfirmAsync("Excluir macro", message, "Excluir"))
            return;
        if (used)
            _host.Update(Section.Buttons, p => p.DeleteMacro(macro.Id));
        else
            _host.UpdateLocal(p => p.DeleteMacro(macro.Id));
        LoadMacros();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task InsertKeyAsync()
    {
        var box = new KeyCaptureBox();
        if (!await Dialogs.ShowContentAsync("Inserir tecla", box, "Inserir") || box.HidUsage == 0)
            return;
        int delay = DefaultDelay;
        var bits = Enumerable.Range(0, 8).Select(i => (byte)(1 << i)).Where(bit => (box.Modifiers & bit) != 0).ToList();
        var list = new List<MacroEvent>();
        list.AddRange(bits.Select(bit => new MacroEvent(MacroEventKind.Modifier, bit, false, delay)));
        list.Add(new MacroEvent(MacroEventKind.Key, box.HidUsage, false, delay));
        list.Add(new MacroEvent(MacroEventKind.Key, box.HidUsage, true, delay));
        list.AddRange(Enumerable.Reverse(bits).Select(bit => new MacroEvent(MacroEventKind.Modifier, bit, true, delay)));
        await InsertEventsAsync(list);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task InsertClickAsync()
    {
        var options = new[]
        {
            ("Clique esquerdo", MouseButtons.Left), ("Clique direito", MouseButtons.Right),
            ("Clique do meio", MouseButtons.Middle), ("Botão voltar", MouseButtons.Back), ("Botão avançar", MouseButtons.Forward),
        };
        var combo = new ComboBox { ItemsSource = options.Select(o => o.Item1).ToList(), SelectedIndex = 0, MinWidth = 240 };
        if (!await Dialogs.ShowContentAsync("Inserir clique", combo, "Inserir"))
            return;
        byte mask = options[Math.Max(0, combo.SelectedIndex)].Item2;
        await InsertEventsAsync([
            new MacroEvent(MacroEventKind.Mouse, mask, false, DefaultDelay),
            new MacroEvent(MacroEventKind.Mouse, mask, true, DefaultDelay),
        ]);
    }

    private async Task InsertEventsAsync(IReadOnlyList<MacroEvent> list)
    {
        if (Events.Count + list.Count > Payloads.MaxMacroEvents)
        {
            await Dialogs.InfoAsync("Macro cheia", $"Uma macro pode ter no máximo {Payloads.MaxMacroEvents} eventos.");
            return;
        }
        int at = SelectedEvent is null ? Events.Count : Events.IndexOf(SelectedEvent) + 1;
        foreach (var e in list)
            Events.Insert(at++, new MacroEventItem(this, e));
        SaveEvents();
    }

    [RelayCommand(CanExecute = nameof(HasEvent))]
    private void MoveUp()
    {
        int i = Events.IndexOf(SelectedEvent!);
        if (i <= 0)
            return;
        Events.Move(i, i - 1);
        SaveEvents();
    }

    [RelayCommand(CanExecute = nameof(HasEvent))]
    private void MoveDown()
    {
        int i = Events.IndexOf(SelectedEvent!);
        if (i < 0 || i >= Events.Count - 1)
            return;
        Events.Move(i, i + 1);
        SaveEvents();
    }

    [RelayCommand(CanExecute = nameof(HasEvent))]
    private void DeleteEvent()
    {
        int i = Events.IndexOf(SelectedEvent!);
        Events.RemoveAt(i);
        SelectedEvent = Events.Count == 0 ? null : Events[Math.Min(i, Events.Count - 1)];
        SaveEvents();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
        {
            StopRecording(save: true);
            return;
        }
        try
        {
            var owner = new System.Windows.Interop.WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
            _hook = new LowLevelKeyboardHook(owner);
        }
        catch (Win32Exception ex)
        {
            await Dialogs.InfoAsync("Não foi possível gravar", ex.Message);
            return;
        }
        _recorder = new MacroRecorder();
        _hook.KeyEvent += OnHookKey;
        IsRecording = true;
        EventCountText = "0 eventos gravados";
        RefreshCommands();
    }

    private void OnHookKey(int vk, bool up, long timeMs)
    {
        if (_recorder is null)
            return;
        if (vk == 0x1B) // Esc: segurar 1 s para parar; toque curto é gravado
        {
            if (!up)
            {
                if (_escDownAt < 0)
                {
                    _escDownAt = timeMs;
                    _escTimer.Start();
                }
                return;
            }
            _escTimer.Stop();
            if (_escDownAt >= 0)
            {
                _recorder.Add(MacroEventKind.Key, 0x29, false, _escDownAt);
                _recorder.Add(MacroEventKind.Key, 0x29, true, timeMs);
            }
            _escDownAt = -1;
            UpdateRecordingCount();
            return;
        }
        if (KeyMap.MacroCodeForVk(vk) is { } code)
        {
            _recorder.Add(code.Kind, code.Code, up, timeMs);
            UpdateRecordingCount();
        }
    }

    public void RecordMouse(byte mask, bool up)
    {
        if (_recorder is null)
            return;
        _recorder.Add(MacroEventKind.Mouse, mask, up, Environment.TickCount64);
        UpdateRecordingCount();
    }

    private void UpdateRecordingCount() =>
        EventCountText = $"{_recorder!.Count} eventos gravados" + (_recorder.IsFull ? " (limite atingido)" : "");

    public void StopRecording(bool save)
    {
        if (!IsRecording)
            return;
        _escTimer.Stop();
        _escDownAt = -1;
        _hook?.Dispose();
        _hook = null;
        IsRecording = false;
        var recorder = _recorder;
        _recorder = null;
        if (save && recorder is { Count: > 0 })
        {
            _loadingEvents = true;
            Events.Clear();
            foreach (var e in recorder.Build(UseRealDelays, DefaultDelay))
                Events.Add(new MacroEventItem(this, e));
            _loadingEvents = false;
            SaveEvents();
        }
        else
        {
            UpdateCount();
        }
        RefreshCommands();
    }
}
