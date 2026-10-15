using System.Windows;
using System.Windows.Controls;
using M800.App.Common;
using M800.Core.Model;
using M800.Core.Protocol;

namespace M800.App.Controls;

public partial class ActionPicker : UserControl
{
    private enum Kind { List, CustomKey, Fire, Macro }

    private sealed record Category(string Name, Kind Kind, IReadOnlyList<ActionOption> Options);

    private readonly List<Category> _categories =
    [
        new("Clique do mouse", Kind.List, ActionCatalog.MouseClicks),
        new("DPI", Kind.List, ActionCatalog.Dpi),
        new("Multimídia", Kind.List, ActionCatalog.Media),
        new("Atalhos prontos", Kind.List, ActionCatalog.Shortcuts),
        new("Tecla ou atalho", Kind.CustomKey, []),
        new("Tecla de tiro", Kind.Fire, []),
        new("Macro", Kind.Macro, []),
        new("Outros", Kind.List, ActionCatalog.Other),
    ];

    public ActionPicker(ButtonBinding current, IReadOnlyList<MacroDefinition> macros)
    {
        InitializeComponent();
        CategoryList.ItemsSource = _categories;
        MacroCombo.ItemsSource = macros;
        NoMacrosText.Visibility = macros.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Select(current, macros);
    }

    private Category Find(Kind kind) => _categories.First(c => c.Kind == kind);

    private void Select(ButtonBinding current, IReadOnlyList<MacroDefinition> macros)
    {
        if (current.MacroId is Guid id && macros.FirstOrDefault(m => m.Id == id) is { } macro)
        {
            CategoryList.SelectedItem = Find(Kind.Macro);
            MacroCombo.SelectedItem = macro;
            MacroRepeat.Value = current.MacroRepeat;
            return;
        }
        var action = current.Action;
        foreach (var category in _categories.Where(c => c.Kind == Kind.List))
        {
            var option = category.Options.FirstOrDefault(o => o.Action == action);
            if (option is not null)
            {
                CategoryList.SelectedItem = category;
                OptionList.SelectedItem = option;
                return;
            }
        }
        if (action.Type == ButtonAction.TypeFire)
        {
            CategoryList.SelectedItem = Find(Kind.Fire);
            FireInterval.Value = action.B1;
            FireRepeat.Value = action.B2;
        }
        else if (action.Type == ButtonAction.TypeKey)
        {
            CategoryList.SelectedItem = Find(Kind.CustomKey);
            KeyBox.Set(action.B1, action.B2);
        }
        else
        {
            CategoryList.SelectedIndex = 0;
        }
    }

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        var kind = (CategoryList.SelectedItem as Category)?.Kind;
        OptionList.Visibility = kind == Kind.List ? Visibility.Visible : Visibility.Collapsed;
        CustomKeyPanel.Visibility = kind == Kind.CustomKey ? Visibility.Visible : Visibility.Collapsed;
        FirePanel.Visibility = kind == Kind.Fire ? Visibility.Visible : Visibility.Collapsed;
        MacroPanel.Visibility = kind == Kind.Macro ? Visibility.Visible : Visibility.Collapsed;
        if (CategoryList.SelectedItem is Category { Kind: Kind.List } category)
        {
            OptionList.ItemsSource = category.Options;
            OptionList.SelectedIndex = -1;
        }
        if (kind == Kind.Macro && MacroCombo.SelectedIndex < 0 && MacroCombo.Items.Count > 0)
            MacroCombo.SelectedIndex = 0;
        if (kind == Kind.CustomKey)
            KeyBox.Focus();
    }

    /// <returns>a ação escolhida, ou null se a escolha estiver incompleta.</returns>
    public ButtonBinding? BuildBinding()
    {
        switch ((CategoryList.SelectedItem as Category)?.Kind)
        {
            case Kind.List:
                return OptionList.SelectedItem is ActionOption option ? ButtonBinding.For(option.Action) : null;
            case Kind.CustomKey:
                return KeyBox.HidUsage == 0 ? null : ButtonBinding.For(ButtonAction.Key(KeyBox.HidUsage, KeyBox.Modifiers));
            case Kind.Fire:
                return ButtonBinding.For(ButtonAction.Fire(
                    (int)Math.Clamp(FireInterval.Value ?? 50, 5, 255),
                    (int)Math.Clamp(FireRepeat.Value ?? 0, 0, 255)));
            case Kind.Macro:
                return MacroCombo.SelectedItem is MacroDefinition macro
                    ? ButtonBinding.ForMacro(macro.Id, (int)Math.Clamp(MacroRepeat.Value ?? 1, 1, 255))
                    : null;
            default:
                return null;
        }
    }
}
