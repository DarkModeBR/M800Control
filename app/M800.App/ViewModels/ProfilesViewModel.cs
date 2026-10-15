using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using M800.App.Common;
using M800.Core.Model;
using M800.Core.Protocol;
using M800.Core.Services;
using Microsoft.Win32;

namespace M800.App.ViewModels;

public sealed record ProfileItem(Guid Id, string Name, string Summary, bool IsActive);

public sealed partial class ProfilesViewModel : ObservableObject
{
    private const string FileFilter = "Perfil do M800 Control (*.m800profile)|*.m800profile|Todos os arquivos (*.*)|*.*";
    private readonly AppHost _host;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivateCommand), nameof(DuplicateCommand), nameof(RenameCommand),
        nameof(DeleteCommand), nameof(ExportCommand))]
    private ProfileItem? _selected;

    public ProfilesViewModel(AppHost host)
    {
        _host = host;
        host.ProfilesChanged += Load;
        host.ActiveProfileChanged += Load;
        host.ProfileEdited += Load;
        Load();
    }

    public ObservableCollection<ProfileItem> Profiles { get; } = [];

    public void Load()
    {
        var selected = Selected?.Id ?? _host.Store.ActiveProfileId;
        Profiles.Clear();
        foreach (var p in _host.Store.Profiles)
            Profiles.Add(new ProfileItem(p.Id, p.Name, Summary(p), p.Id == _host.Store.ActiveProfileId));
        Selected = Profiles.FirstOrDefault(x => x.Id == selected) ?? Profiles.FirstOrDefault();
    }

    private static string Summary(Profile p)
    {
        int level = ProfileEncoder.ActiveLevel(p);
        return $"{p.DpiLevels[level].Dpi:N0} DPI · {p.PollingRateHz} Hz · {LightingModes.DisplayName(p.Lighting.Mode)} · {p.Macros.Count} macro(s)";
    }

    private bool HasSelection() => Selected is not null;

    private Profile? SelectedProfile => Selected is null ? null : _host.Store.Profiles.FirstOrDefault(p => p.Id == Selected.Id);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Activate() => _host.ActivateProfile(Selected!.Id);

    [RelayCommand]
    private async Task NewAsync()
    {
        var name = await Dialogs.PromptAsync("Novo perfil", "Novo perfil");
        if (name is null)
            return;
        var profile = _host.Store.Add(Profile.CreateDefault(name));
        _host.ProfilesEdited();
        Selected = Profiles.FirstOrDefault(x => x.Id == profile.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Duplicate()
    {
        var source = SelectedProfile!;
        var copy = _host.Store.Add(source.Duplicate(source.Name + " (cópia)"));
        _host.ProfilesEdited();
        Selected = Profiles.FirstOrDefault(x => x.Id == copy.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RenameAsync()
    {
        var profile = SelectedProfile!;
        var name = await Dialogs.PromptAsync("Renomear perfil", profile.Name);
        if (name is null || name == profile.Name)
            return;
        profile.Name = _host.Store.UniqueName(name);
        _host.ProfilesEdited();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        var profile = SelectedProfile!;
        if (profile.Id == _host.Store.ActiveProfileId)
        {
            await Dialogs.InfoAsync("Perfil ativo", "Ative outro perfil antes de excluir este.");
            return;
        }
        if (!await Dialogs.ConfirmAsync("Excluir perfil", $"Excluir o perfil \"{profile.Name}\"? Isso não pode ser desfeito.", "Excluir"))
            return;
        try
        {
            _host.Store.Delete(profile.Id);
            _host.ProfilesEdited();
        }
        catch (InvalidOperationException ex)
        {
            await Dialogs.InfoAsync("Não foi possível excluir", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog { Filter = FileFilter, Title = "Importar perfil" };
        if (dialog.ShowDialog() != true)
            return;
        try
        {
            var profile = _host.Store.Import(dialog.FileName);
            _host.ProfilesEdited();
            Selected = Profiles.FirstOrDefault(x => x.Id == profile.Id);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            await Dialogs.InfoAsync("Não foi possível importar", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ExportAsync()
    {
        var profile = SelectedProfile!;
        var dialog = new SaveFileDialog { Filter = FileFilter, FileName = profile.Name + ".m800profile", Title = "Exportar perfil" };
        if (dialog.ShowDialog() != true)
            return;
        try
        {
            _host.Store.Export(profile, dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.InfoAsync("Não foi possível exportar", ex.Message);
        }
    }

    [RelayCommand]
    private void Resend() => _host.ResendActive();
}
