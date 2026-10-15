using System.Windows.Input;
using M800.App.Common;
using M800.App.Platform;
using M800.Core.Protocol;

namespace M800.App.Controls;

/// <summary>Campo que captura uma tecla com modificadores (Ctrl, Shift, Alt).</summary>
public sealed class KeyCaptureBox : Wpf.Ui.Controls.TextBox
{
    public KeyCaptureBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        PlaceholderText = "Clique aqui e pressione a tecla ou combinação";
        MinWidth = 320;
        Loaded += (_, _) => Focus();
    }

    public byte Modifiers { get; private set; }
    public byte HidUsage { get; private set; }

    public void Set(byte modifiers, byte usage)
    {
        Modifiers = modifiers;
        HidUsage = usage;
        Text = usage == 0 ? "" : ActionCatalog.KeyText(modifiers, usage, KeyNames.Name);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        byte hid = KeyMap.VkToHid(KeyInterop.VirtualKeyFromKey(key));
        if (hid == 0 || KeyMap.IsModifier(hid))
            return; // espera a tecla principal
        byte mods = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl)) mods |= M800.Core.Protocol.Modifiers.LCtrl;
        if (Keyboard.IsKeyDown(Key.LeftShift)) mods |= M800.Core.Protocol.Modifiers.LShift;
        if (Keyboard.IsKeyDown(Key.LeftAlt)) mods |= M800.Core.Protocol.Modifiers.LAlt;
        if (Keyboard.IsKeyDown(Key.RightCtrl)) mods |= M800.Core.Protocol.Modifiers.RCtrl;
        if (Keyboard.IsKeyDown(Key.RightShift)) mods |= M800.Core.Protocol.Modifiers.RShift;
        if (Keyboard.IsKeyDown(Key.RightAlt)) mods |= M800.Core.Protocol.Modifiers.RAlt;
        Set(mods, hid);
    }
}
