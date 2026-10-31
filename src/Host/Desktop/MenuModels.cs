using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace MDGsm.Desktop;

public sealed class MenuCatalogDocument
{
    public string CatalogVersion { get; set; } = "1.0";
    public List<MenuRoot> MainMenus { get; set; } = [];
    public List<MenuAction> QuickActions { get; set; } = [];
}

public sealed class MenuRoot
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<MenuGroup> Groups { get; set; } = [];

    [JsonIgnore]
    public string IconGlyph => Id switch
    {
        "sales" => "🛒",
        "treasury" => "▣",
        "inventory" => "▦",
        "accounting" => "▥",
        "payroll" => "♙",
        "tax" => "▥",
        "distribution" => "⇄",
        "base-data" => "▣",
        "utilities" => "⚒",
        "settings" => "⚙",
        "help" => "?",
        _ => "•"
    };

    [JsonIgnore]
    public Brush AccentBrush => Id switch
    {
        "sales" => CreateAccent(96, 165, 250),
        "treasury" => CreateAccent(52, 211, 153),
        "inventory" => CreateAccent(251, 191, 36),
        "accounting" => CreateAccent(192, 132, 252),
        "payroll" => CreateAccent(34, 211, 238),
        "tax" => CreateAccent(251, 113, 133),
        "distribution" => CreateAccent(45, 212, 191),
        "base-data" => CreateAccent(129, 140, 248),
        "utilities" => CreateAccent(253, 224, 71),
        "settings" => CreateAccent(148, 163, 184),
        "help" => CreateAccent(56, 189, 248),
        _ => CreateAccent(203, 213, 225)
    };

    private static Brush CreateAccent(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}

public sealed class MenuGroup
{
    public string Title { get; set; } = string.Empty;
    // direct = render children as menu rows; submenu = render a cascade row; hidden = retain catalog metadata without showing it.
    public string DisplayMode { get; set; } = "submenu";
    public bool SeparatorBefore { get; set; }
    public List<MenuAction> Items { get; set; } = [];
}

public sealed class MenuAction
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OwnerDomain { get; set; } = string.Empty;
    public string CatalogStatus { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string SecurityScope { get; set; } = string.Empty;
    public string Shortcut { get; set; } = string.Empty;
    public bool SeparatorBefore { get; set; }
}

public sealed class MenuOptionViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string OwnerDomain { get; init; } = string.Empty;
    public string SourceDescription { get; init; } = string.Empty;
    public string StatusText { get; init; } = "در دست طراحی";
}

public sealed class MenuGroupViewModel
{
    public string Title { get; init; } = string.Empty;
    public ObservableCollection<MenuOptionViewModel> Items { get; init; } = [];
}
