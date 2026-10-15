using System.Windows;
using Wpf.Ui.Controls;

namespace M800.App.Common;

/// <summary>Diálogos no estilo Windows 11 (ContentDialog do WPF-UI) sobre a janela principal.</summary>
public static class Dialogs
{
    public static ContentDialogHost? Host { get; set; }

    private static ContentDialog Create(string title, object content, string? primary, string close, double maxWidth = 560) => new(Host!)
    {
        Title = title,
        Content = content,
        PrimaryButtonText = primary ?? "",
        CloseButtonText = close,
        DefaultButton = primary is null ? ContentDialogButton.Close : ContentDialogButton.Primary,
        DialogMaxWidth = maxWidth,
    };

    private static TextBlock Text(string message) =>
        new() { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 480 };

    public static async Task<bool> ConfirmAsync(string title, string message, string primary, string close = "Cancelar") =>
        await Create(title, Text(message), primary, close).ShowAsync() == ContentDialogResult.Primary;

    public static async Task InfoAsync(string title, string message) =>
        await Create(title, Text(message), null, "OK").ShowAsync();

    public static async Task<string?> PromptAsync(string title, string initial)
    {
        var box = new TextBox { Text = initial, MinWidth = 340 };
        var dialog = Create(title, box, "OK", "Cancelar");
        dialog.Loaded += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(box.Text) ? box.Text.Trim() : null;
    }

    public static async Task<bool> ShowContentAsync(string title, object content, string primary, string close = "Cancelar", double maxWidth = 560) =>
        await Create(title, content, primary, close, maxWidth).ShowAsync() == ContentDialogResult.Primary;
}
