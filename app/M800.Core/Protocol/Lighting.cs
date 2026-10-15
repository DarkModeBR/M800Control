namespace M800.Core.Protocol;

public enum LightingMode : byte
{
    Off = 0,
    ColorfulStreaming = 1,
    Breathing = 2,
    Steady = 3,
    Neon = 4,
    Snake = 5,
    ColorfulSteady = 6,
    Flicker = 7,
    StarsTwinkle = 8,
    Wave = 9,
}

public static class LightingModes
{
    public static readonly IReadOnlyList<LightingMode> All = Enum.GetValues<LightingMode>();

    public static string DisplayName(LightingMode mode) => mode switch
    {
        LightingMode.Off => "Desligado",
        LightingMode.ColorfulStreaming => "Arco-íris em movimento",
        LightingMode.Breathing => "Respiração",
        LightingMode.Steady => "Cor fixa",
        LightingMode.Neon => "Neon",
        LightingMode.Snake => "Cobra",
        LightingMode.ColorfulSteady => "Arco-íris fixo",
        LightingMode.Flicker => "Piscar",
        LightingMode.StarsTwinkle => "Estrelas",
        LightingMode.Wave => "Onda",
        _ => mode.ToString(),
    };

    public static int MaxColors(LightingMode mode) => mode switch
    {
        LightingMode.Breathing => 7,
        LightingMode.Steady => 1,
        LightingMode.ColorfulSteady => 7,
        LightingMode.Flicker => 2,
        _ => 0,
    };

    public static int MinColors(LightingMode mode) => mode == LightingMode.Breathing ? 1 : MaxColors(mode);

    public static bool UsesDirection(LightingMode mode) => mode == LightingMode.ColorfulStreaming;

    /// <summary>O driver oficial desabilita brilho e velocidade nesses modos.</summary>
    public static bool UsesSpeedAndBrightness(LightingMode mode) =>
        mode is not (LightingMode.Off or LightingMode.Steady or LightingMode.ColorfulSteady);
}
