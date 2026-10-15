using M800.Core.Model;
using M800.Core.Protocol;

namespace M800.App.Common;

public sealed record ActionOption(string Name, ButtonAction Action);

/// <summary>Ações oferecidas na interface (tabelas do driver oficial, PROTOCOLO.md §6).</summary>
public static class ActionCatalog
{
    public static readonly IReadOnlyList<ActionOption> MouseClicks =
    [
        new("Clique esquerdo", ButtonAction.Mouse(MouseButtons.Left)),
        new("Clique direito", ButtonAction.Mouse(MouseButtons.Right)),
        new("Clique do meio", ButtonAction.Mouse(MouseButtons.Middle)),
        new("Voltar", ButtonAction.Mouse(MouseButtons.Back)),
        new("Avançar", ButtonAction.Mouse(MouseButtons.Forward)),
    ];

    public static readonly IReadOnlyList<ActionOption> Dpi =
    [
        new("Alternar DPI (ciclo)", ButtonAction.DpiLoop),
        new("DPI +", ButtonAction.DpiUp),
        new("DPI −", ButtonAction.DpiDown),
    ];

    public static readonly IReadOnlyList<ActionOption> Media =
    [
        new("Reproduzir / pausar", ButtonAction.Consumer(ConsumerKeys.PlayPause)),
        new("Próxima faixa", ButtonAction.Consumer(ConsumerKeys.NextTrack)),
        new("Faixa anterior", ButtonAction.Consumer(ConsumerKeys.PrevTrack)),
        new("Parar", ButtonAction.Consumer(ConsumerKeys.Stop)),
        new("Volume +", ButtonAction.Consumer(ConsumerKeys.VolumeUp)),
        new("Volume −", ButtonAction.Consumer(ConsumerKeys.VolumeDown)),
        new("Mudo", ButtonAction.Consumer(ConsumerKeys.Mute)),
        new("Player de mídia", ButtonAction.Consumer(ConsumerKeys.MediaSelect)),
        new("E-mail", ButtonAction.Consumer(ConsumerKeys.Mail)),
        new("Calculadora", ButtonAction.Consumer(ConsumerKeys.Calculator)),
    ];

    public static readonly IReadOnlyList<ActionOption> Shortcuts =
    [
        new("Copiar (Ctrl + C)", ButtonAction.Key(0x06, Modifiers.LCtrl)),
        new("Colar (Ctrl + V)", ButtonAction.Key(0x19, Modifiers.LCtrl)),
        new("Recortar (Ctrl + X)", ButtonAction.Key(0x1B, Modifiers.LCtrl)),
        new("Desfazer (Ctrl + Z)", ButtonAction.Key(0x1D, Modifiers.LCtrl)),
        new("Selecionar tudo (Ctrl + A)", ButtonAction.Key(0x04, Modifiers.LCtrl)),
        new("Novo (Ctrl + N)", ButtonAction.Key(0x11, Modifiers.LCtrl)),
        new("Salvar (Ctrl + S)", ButtonAction.Key(0x16, Modifiers.LCtrl)),
        new("Imprimir (Ctrl + P)", ButtonAction.Key(0x13, Modifiers.LCtrl)),
        new("Abrir o Explorador (Win + E)", ButtonAction.Key(0x08, Modifiers.LWin)),
        new("Meu computador", ButtonAction.Consumer(ConsumerKeys.MyComputer)),
        new("Fechar janela (Alt + F4)", ButtonAction.Key(0x3D, Modifiers.LAlt)),
        new("Mostrar a área de trabalho (Win + D)", ButtonAction.Key(0x07, Modifiers.LWin)),
        new("Executar (Win + R)", ButtonAction.Key(0x15, Modifiers.LWin)),
        new("Minimizar janela (Win + ↓)", ButtonAction.Key(0x51, Modifiers.LWin)),
        new("Maximizar janela (Win + ↑)", ButtonAction.Key(0x52, Modifiers.LWin)),
    ];

    public static readonly IReadOnlyList<ActionOption> Other =
    [
        new("Clique triplo", ButtonAction.TripleClick),
        new("Trocar efeito de luz", ButtonAction.SwitchLighting),
        new("Desativado", ButtonAction.Disabled),
    ];

    public static readonly IReadOnlyList<ActionOption> All = [.. MouseClicks, .. Dpi, .. Media, .. Shortcuts, .. Other];

    public static string Describe(ButtonBinding binding, IReadOnlyList<MacroDefinition> macros, Func<byte, string> keyName)
    {
        if (binding.MacroId is Guid id)
        {
            var macro = macros.FirstOrDefault(m => m.Id == id);
            if (macro is null)
                return "Macro removida";
            return binding.MacroRepeat > 1 ? $"Macro: {macro.Name} ({binding.MacroRepeat}×)" : $"Macro: {macro.Name}";
        }
        var action = binding.Action;
        var known = All.FirstOrDefault(o => o.Action == action);
        if (known is not null)
            return known.Name;
        return action.Type switch
        {
            ButtonAction.TypeFire => $"Tecla de tiro ({action.B1} ms, {(action.B2 == 0 ? "enquanto segurar" : $"{action.B2} cliques")})",
            ButtonAction.TypeKey => KeyText(action.B1, action.B2, keyName),
            ButtonAction.TypeConsumer => $"Tecla multimídia 0x{action.B2:X2}{action.B1:X2}",
            ButtonAction.TypeMouse => $"Botões do mouse (0x{action.B1:X2})",
            _ => $"Ação 0x{action.Type:X2}",
        };
    }

    public static string KeyText(byte modifiers, byte usage, Func<byte, string> keyName) =>
        string.Join(" + ", KeyMap.ModifierNames(modifiers).Append(keyName(usage)));
}
