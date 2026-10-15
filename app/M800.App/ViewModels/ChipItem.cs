using CommunityToolkit.Mvvm.Input;

namespace M800.App.ViewModels;

public sealed record ChipItem(string Label, string Detail, bool IsActive, IRelayCommand Command);
