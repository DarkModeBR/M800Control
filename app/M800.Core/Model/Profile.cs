using System.Text.Json;
using M800.Core.Protocol;

namespace M800.Core.Model;

public sealed class DpiLevel
{
    public int Dpi { get; set; } = 800;
    public bool Enabled { get; set; } = true;
    public Rgb Color { get; set; } = Rgb.White;
}

public sealed class LightingSettings
{
    private static readonly Rgb Red = new(255, 0, 0), Green = new(0, 255, 0), Blue = new(0, 0, 255),
        Cyan = new(0, 255, 255), Yellow = new(255, 255, 0), Magenta = new(255, 0, 255);

    public LightingMode Mode { get; set; } = LightingMode.ColorfulStreaming;
    public int Brightness { get; set; } = 2;
    public int Speed { get; set; } = 2;
    public int Direction { get; set; }
    public Dictionary<LightingMode, List<Rgb>> Colors { get; set; } = DefaultColors();

    /// <summary>Cores do modo (guardadas por modo para não se perderem ao alternar).</summary>
    public List<Rgb> ColorsFor(LightingMode mode)
    {
        if (!Colors.TryGetValue(mode, out var list))
        {
            list = DefaultColors().TryGetValue(mode, out var d) ? d : [];
            Colors[mode] = list;
        }
        return list;
    }

    public static Dictionary<LightingMode, List<Rgb>> DefaultColors() => new()
    {
        [LightingMode.Breathing] = [Red, Green, Blue, Cyan, Yellow, Magenta, Rgb.White],
        [LightingMode.Steady] = [new Rgb(0x60, 0xCD, 0xFF)],
        [LightingMode.ColorfulSteady] = [Red, Green, Blue, Cyan, Yellow, Magenta, Rgb.White],
        [LightingMode.Flicker] = [Red, Blue],
    };
}

public sealed class ButtonBinding
{
    public ButtonAction Action { get; set; }

    /// <summary>Quando definido, o botão executa esta macro (Action é ignorada).</summary>
    public Guid? MacroId { get; set; }

    public int MacroRepeat { get; set; } = 1;

    public static ButtonBinding For(ButtonAction action) => new() { Action = action };

    public static ButtonBinding ForMacro(Guid macroId, int repeat) =>
        new() { MacroId = macroId, MacroRepeat = Math.Clamp(repeat, 1, 255) };
}

public sealed class MacroDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nova macro";
    public List<MacroEvent> Events { get; set; } = [];
}

public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Padrão";
    public List<DpiLevel> DpiLevels { get; set; } = [];
    public int ActiveDpiLevel { get; set; }
    public int PollingRateHz { get; set; } = 500;
    public LightingSettings Lighting { get; set; } = new();
    public int LiftOffMm { get; set; } = 2;
    public int DebounceMs { get; set; } = 12;
    public int SleepMinutes { get; set; } = 6;
    public bool LightOffWhileMoving { get; set; }
    public bool MotionWakeup { get; set; } = true;
    public List<ButtonBinding> Buttons { get; set; } = [];
    public List<MacroDefinition> Macros { get; set; } = [];
    public PointerSettings Pointer { get; set; } = new();

    public static Profile CreateDefault(string name = "Padrão") => new()
    {
        Name = name,
        DpiLevels =
        [
            new() { Dpi = 400, Color = new Rgb(255, 0, 0) },
            new() { Dpi = 800, Color = new Rgb(0, 255, 0) },
            new() { Dpi = 1600, Color = new Rgb(0, 0, 255) },
            new() { Dpi = 3200, Color = new Rgb(0, 255, 255) },
            new() { Dpi = 6400, Color = new Rgb(255, 255, 0) },
            new() { Dpi = 12800, Enabled = false, Color = new Rgb(255, 0, 255) },
        ],
        ActiveDpiLevel = 2,
        Buttons = ButtonIds.All.Select(b => ButtonBinding.For(ButtonAction.DefaultFor(b))).ToList(),
    };

    public Profile Clone() =>
        JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(this, Json.Options), Json.Options)!;

    public Profile Duplicate(string name)
    {
        var copy = Clone();
        copy.Id = Guid.NewGuid();
        copy.Name = name;
        return copy;
    }

    public bool HasLeftClick =>
        Buttons.Any(b => b.MacroId is null && b.Action == ButtonAction.Mouse(MouseButtons.Left));

    public bool IsMacroUsed(Guid macroId) => Buttons.Any(b => b.MacroId == macroId);

    public void DeleteMacro(Guid macroId)
    {
        Macros.RemoveAll(m => m.Id == macroId);
        for (int i = 0; i < Buttons.Count; i++)
        {
            if (Buttons[i].MacroId == macroId)
                Buttons[i] = ButtonBinding.For(ButtonAction.DefaultFor((ButtonId)i));
        }
    }

    /// <summary>Corrige dados incompletos ou fora da faixa (arquivo antigo, editado à mão, importado).</summary>
    public void Normalize()
    {
        var defaults = CreateDefault();
        DpiLevels ??= [];
        while (DpiLevels.Count < ProtocolConstants.MaxDpiLevels)
            DpiLevels.Add(defaults.DpiLevels[DpiLevels.Count]);
        if (DpiLevels.Count > ProtocolConstants.MaxDpiLevels)
            DpiLevels.RemoveRange(ProtocolConstants.MaxDpiLevels, DpiLevels.Count - ProtocolConstants.MaxDpiLevels);
        if (!DpiLevels.Any(l => l.Enabled))
            DpiLevels[0].Enabled = true;
        if (ActiveDpiLevel < 0 || ActiveDpiLevel >= DpiLevels.Count || !DpiLevels[ActiveDpiLevel].Enabled)
            ActiveDpiLevel = DpiLevels.FindIndex(l => l.Enabled);

        Macros ??= [];
        Buttons ??= [];
        while (Buttons.Count < ButtonIds.All.Count)
            Buttons.Add(defaults.Buttons[Buttons.Count]);
        if (Buttons.Count > ButtonIds.All.Count)
            Buttons.RemoveRange(ButtonIds.All.Count, Buttons.Count - ButtonIds.All.Count);
        for (int i = 0; i < Buttons.Count; i++)
        {
            if (Buttons[i].MacroId is Guid id && Macros.All(m => m.Id != id))
                Buttons[i] = ButtonBinding.For(ButtonAction.DefaultFor((ButtonId)i));
            Buttons[i].MacroRepeat = Math.Clamp(Buttons[i].MacroRepeat, 1, 255);
        }
        foreach (var m in Macros)
        {
            m.Events ??= [];
            if (m.Events.Count > Payloads.MaxMacroEvents)
                m.Events.RemoveRange(Payloads.MaxMacroEvents, m.Events.Count - Payloads.MaxMacroEvents);
        }

        Pointer ??= new PointerSettings();
        Pointer.Normalize();

        Lighting ??= new LightingSettings();
        Lighting.Colors ??= LightingSettings.DefaultColors();
        Lighting.Brightness = Math.Clamp(Lighting.Brightness, 1, 4);
        Lighting.Speed = Math.Clamp(Lighting.Speed, 1, 3);
        Lighting.Direction = Lighting.Direction == 0 ? 0 : 1;

        if (!Payloads.PollingRates.Contains(PollingRateHz))
            PollingRateHz = 500;
        LiftOffMm = Math.Clamp(LiftOffMm, 1, 2);
        DebounceMs = Math.Clamp(DebounceMs - DebounceMs % 2, 0, 30);
        SleepMinutes = Math.Clamp(SleepMinutes, 1, 255);
        Name = string.IsNullOrWhiteSpace(Name) ? "Perfil" : Name.Trim();
    }
}
