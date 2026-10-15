namespace M800.Core.Model;

/// <summary>Partes da configuração que são enviadas ao mouse de forma independente.</summary>
public enum Section { Dpi, DpiColors, Polling, Lighting, LiftOff, Debounce, Power, MotionWakeup, Buttons }

public static class Sections
{
    public static readonly Section[] All = Enum.GetValues<Section>();

    public static string DisplayName(Section s) => s switch
    {
        Section.Dpi => "DPI",
        Section.DpiColors => "Cores do DPI",
        Section.Polling => "Polling rate",
        Section.Lighting => "Iluminação",
        Section.LiftOff => "LOD",
        Section.Debounce => "Debounce",
        Section.Power => "Energia",
        Section.MotionWakeup => "Acordar ao mover",
        Section.Buttons => "Botões e macros",
        _ => s.ToString(),
    };
}
