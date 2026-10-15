using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using M800.App.Common;
using M800.App.Controls;
using M800.App.Platform;
using M800.Core.Model;
using M800.Core.Protocol;

namespace M800.App.ViewModels;

public sealed record ButtonRow(ButtonId Id, string Name, string Action);

public sealed partial class ButtonsViewModel : ObservableObject
{
    private readonly AppHost _host;

    public ButtonsViewModel(AppHost host)
    {
        _host = host;
        host.ActiveProfileChanged += Load;
        host.ProfileEdited += Load;
        Load();
    }

    public ObservableCollection<ButtonRow> Rows { get; } = [];

    public void Load()
    {
        var profile = _host.Active;
        Rows.Clear();
        foreach (var id in ButtonIds.All)
            Rows.Add(new ButtonRow(id, ButtonIds.DisplayName(id),
                ActionCatalog.Describe(profile.Buttons[(int)id], profile.Macros, KeyNames.Name)));
    }

    [RelayCommand]
    private async Task EditAsync(ButtonId id)
    {
        var profile = _host.Active;
        var picker = new ActionPicker(profile.Buttons[(int)id], profile.Macros);
        if (!await Dialogs.ShowContentAsync($"{ButtonIds.DisplayName(id)}: escolher ação", picker, "Aplicar", maxWidth: 760))
            return;
        var binding = picker.BuildBinding();
        if (binding is null)
        {
            await Dialogs.InfoAsync("Escolha incompleta", "Selecione uma ação, pressione uma tecla ou escolha uma macro antes de aplicar.");
            return;
        }
        var preview = profile.Clone();
        preview.Buttons[(int)id] = binding;
        if (!preview.HasLeftClick)
        {
            await Dialogs.InfoAsync("Clique esquerdo necessário",
                "Pelo menos um botão precisa continuar como clique esquerdo, senão você ficaria sem clique.");
            return;
        }
        _host.Update(Section.Buttons, p => p.Buttons[(int)id] = binding);
    }

    [RelayCommand]
    private async Task RestoreDefaultsAsync()
    {
        if (!await Dialogs.ConfirmAsync("Restaurar botões", "Todos os botões voltam às funções de fábrica.", "Restaurar"))
            return;
        _host.Update(Section.Buttons, p =>
        {
            for (int i = 0; i < p.Buttons.Count; i++)
                p.Buttons[i] = ButtonBinding.For(ButtonAction.DefaultFor((ButtonId)i));
        });
    }
}
