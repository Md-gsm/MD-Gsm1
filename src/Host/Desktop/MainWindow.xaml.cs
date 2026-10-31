using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MDGsm.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _menuCloseTimer;
    private readonly DispatcherTimer _clockTimer;
    private MenuRoot? _activeMenu;
    private Button? _activeMenuAnchor;
    private Popup? _activeGroupPopup;
    private readonly List<PersonPreviewRow> _allPreviewPersons = [];
    private readonly List<ReceptionRow> _allReceptionRows = [];

    private static readonly SolidColorBrush UrgentMarkerBrush = CreateFrozenBrush("#E00000");
    private static readonly SolidColorBrush PartnerMarkerBrush = CreateFrozenBrush("#8B5CF6");
    private static readonly SolidColorBrush LaterDeliveryMarkerBrush = CreateFrozenBrush("#10B981");
    private static readonly SolidColorBrush TodayDeliveryMarkerBrush = CreateFrozenBrush("#F97316");
    private static readonly SolidColorBrush DefaultMarkerBrush = CreateFrozenBrush("#94A3B8");
    private int _nextPreviewNumber = 5;
    private string _activeReceptionFilter = "deliveryToday";

    public ObservableCollection<MenuRoot> MainMenus { get; } = [];
    public ObservableCollection<PersonPreviewRow> PreviewPersons { get; } = [];
    public ObservableCollection<ReceptionRow> ReceptionRows { get; } = [];
    public ObservableCollection<StatisticBarRow> PartStatistics { get; } = [];
    public ObservableCollection<StatisticBarRow> RepairStatistics { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        SeedPreviewPersons();
        SeedReceptionRows();
        SeedDashboardStatistics();

        var catalog = LoadMenuCatalog();
        foreach (var menu in catalog.MainMenus)
            MainMenus.Add(menu);
        if (MainMenus.Count != 11)
            throw new InvalidDataException($"کاتالوگ باید ۱۱ منوی اصلی داشته باشد؛ تعداد فعلی {MainMenus.Count} است.");

        RefreshPreviewPersons();
        RefreshReceptionRows();
        UpdatePersianClock();

        _menuCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _menuCloseTimer.Tick += MenuCloseTimer_Tick;
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clockTimer.Tick += (_, _) => UpdatePersianClock();
        _clockTimer.Start();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Reassert maximize after WPF measures the startup window so the dashboard uses the current
        // monitor's work area on both laptop panels and external monitors.
        if (WindowState != WindowState.Maximized)
            WindowState = WindowState.Maximized;
        DashboardView.InvalidateMeasure();
        DashboardView.UpdateLayout();
        ApplyDashboardResponsiveLayout();
    }

    private static MenuCatalogDocument LoadMenuCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "menu-catalog-fa.json");
        if (!File.Exists(path))
            throw new InvalidDataException("فایل کاتالوگ منو در پوشهٔ Assets پیدا نشد.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var catalog = JsonSerializer.Deserialize<MenuCatalogDocument>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException("فایل کاتالوگ منو خالی یا نامعتبر است.");

        if (catalog.MainMenus.Count == 0 || catalog.MainMenus.Any(m => string.IsNullOrWhiteSpace(m.Id) || string.IsNullOrWhiteSpace(m.Title)))
            throw new InvalidDataException("کاتالوگ منو دارای منوی اصلی نامعتبر است.");
        if (catalog.MainMenus.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != catalog.MainMenus.Count)
            throw new InvalidDataException("شناسهٔ منوهای اصلی باید یکتا باشد.");

        var invalidGroup = catalog.MainMenus.SelectMany(menu => menu.Groups)
            .FirstOrDefault(group => !new[] { "direct", "submenu", "hidden" }.Contains(group.DisplayMode, StringComparer.OrdinalIgnoreCase));
        if (invalidGroup is not null)
            throw new InvalidDataException($"حالت نمایش گروه «{invalidGroup.Title}» باید direct، submenu یا hidden باشد.");

        var allActions = catalog.MainMenus.SelectMany(menu => menu.Groups).SelectMany(group => group.Items).ToArray();
        if (allActions.Any(action => string.IsNullOrWhiteSpace(action.Id) || string.IsNullOrWhiteSpace(action.Title) || string.IsNullOrWhiteSpace(action.OwnerDomain)))
            throw new InvalidDataException("هر گزینهٔ کاتالوگ باید شناسه، عنوان و مالک دامنه داشته باشد.");
        var duplicateActionIds = allActions.GroupBy(action => action.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicateActionIds is not null)
            throw new InvalidDataException($"شناسهٔ گزینهٔ منو تکراری است: {duplicateActionIds.Key}");

        return catalog;
    }

    // The top-level menu is hover-only. It opens a compact popup beneath the hovered root button.
    // Group rows open a second popup to the LEFT, matching the RTL cascading menus in the reference.
    private void NavMenuButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button anchor || anchor.DataContext is not MenuRoot root)
            return;

        _menuCloseTimer.Stop();
        CloseGroupMenu();

        // Tax/help child lists were not expanded in the supplied Parsian screenshots. Keep their
        // top-level labels in the exact root order, but do not invent an empty or fabricated popup.
        var hasEvidenceBackedContent = root.Groups.Any(group =>
            !group.DisplayMode.Equals("hidden", StringComparison.OrdinalIgnoreCase) &&
            group.Items.Any(item => !IsPlaceholderItem(item)));
        if (!hasEvidenceBackedContent)
        {
            CloseActiveMenu();
            return;
        }

        OpenMenu(root, anchor);
    }

    private void OpenMenu(MenuRoot root, Button anchor)
    {
        CloseActiveMenu();
        _activeMenu = root;
        _activeMenuAnchor = anchor;
        RootMenuPopup.PlacementTarget = anchor;
        RootMenuPopup.Placement = PlacementMode.Custom;
        RootMenuPopup.CustomPopupPlacementCallback = PlaceRootMenuPopup;
        RootMenuPopup.HorizontalOffset = 0;
        RootMenuPopup.VerticalOffset = 0;
        RootMenuPopup.Child = BuildRootMenuPanel(root);
        RootMenuPopup.IsOpen = true;
    }

    private static CustomPopupPlacement[] PlaceRootMenuPopup(Size popupSize, Size targetSize, Point offset)
    {
        // RTL reference alignment: the dropdown's right edge meets the hovered root menu's right edge.
        // The alternate candidate lets WPF keep the popup on-screen near the left edge of the window.
        return
        [
            new CustomPopupPlacement(
                new Point(targetSize.Width - popupSize.Width, targetSize.Height),
                PopupPrimaryAxis.None),
            new CustomPopupPlacement(new Point(0, targetSize.Height), PopupPrimaryAxis.None)
        ];
    }

    private Border BuildRootMenuPanel(MenuRoot root)
    {
        var rows = new StackPanel { FlowDirection = FlowDirection.RightToLeft };
        var hasContent = false;

        foreach (var group in root.Groups)
        {
            if (group.DisplayMode.Equals("hidden", StringComparison.OrdinalIgnoreCase) || group.Items.Count == 0)
                continue;

            if (group.SeparatorBefore && hasContent)
                rows.Children.Add(CreateMenuSeparator());

            switch (group.DisplayMode.Trim().ToLowerInvariant())
            {
                case "direct":
                    foreach (var item in group.Items)
                    {
                        if (IsPlaceholderItem(item))
                            continue;
                        if (item.SeparatorBefore && rows.Children.Count > 0)
                            rows.Children.Add(CreateMenuSeparator());
                        rows.Children.Add(CreateActionButton(item, root.AccentBrush));
                        hasContent = true;
                    }
                    break;
                case "submenu":
                    rows.Children.Add(CreateGroupButton(group));
                    hasContent = true;
                    break;
                default:
                    throw new InvalidDataException($"حالت نمایش منو برای گروه «{group.Title}» معتبر نیست.");
            }
        }

        if (rows.Children.Count == 0)
        {
            rows.Children.Add(new Border
            {
                Padding = new Thickness(10, 8, 10, 8),
                Child = new TextBlock
                {
                    Text = "جزئیات این منو در تصاویر مرجع باز نشده است؛ گزینه‌ای حدس زده نشده.",
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    FontSize = 12.1,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 235
                }
            });
        }

        var workAreaHeight = SystemParameters.WorkArea.Height;
        var maxMenuHeight = Math.Max(300d, Math.Min(640d, workAreaHeight - 90d));
        return CreateMenuPopupBorder(rows, 260, maxMenuHeight);
    }

    private static bool IsPlaceholderItem(MenuAction item) =>
        item.Title.Contains("گروه دارای زیرمنو", StringComparison.Ordinal);

    private Button CreateGroupButton(MenuGroup group)
    {
        var row = new DockPanel { FlowDirection = FlowDirection.RightToLeft, LastChildFill = true };
        var title = new TextBlock
        {
            Text = group.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var arrow = new TextBlock
        {
            Text = "◂",
            FontSize = 15.4,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(7, 0, 0, 0)
        };
        DockPanel.SetDock(arrow, Dock.Left);
        row.Children.Add(arrow);
        row.Children.Add(title);

        var button = new Button
        {
            Style = (Style)FindResource("CascadeMenuButtonStyle"),
            Content = row,
            Tag = group,
            ToolTip = "برای نمایش زیرمنو، نشانگر را روی این گزینه نگه دارید"
        };
        button.MouseEnter += GroupMenuItem_MouseEnter;
        return button;
    }

    private void GroupMenuItem_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button anchor || anchor.Tag is not MenuGroup group)
            return;

        _menuCloseTimer.Stop();
        if (_activeGroupPopup?.IsOpen == true && ReferenceEquals(_activeGroupPopup.PlacementTarget, anchor))
            return;

        CloseGroupMenu();
        var rows = new StackPanel { FlowDirection = FlowDirection.RightToLeft };
        var actionableItems = group.Items.Where(item => !IsPlaceholderItem(item)).ToList();

        if (actionableItems.Count == 0)
        {
            rows.Children.Add(new Border
            {
                Padding = new Thickness(10, 8, 10, 8),
                Child = new TextBlock
                {
                    Text = "گزینه‌های این زیرمنو هنوز در کاتالوگ مصوب موجود نیست.",
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    FontSize = 12.1,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 235
                }
            });
        }
        else
        {
            foreach (var item in actionableItems)
            {
                if (item.SeparatorBefore && rows.Children.Count > 0)
                    rows.Children.Add(CreateMenuSeparator());
                rows.Children.Add(CreateActionButton(item, _activeMenu?.AccentBrush, isNested: true));
            }
        }

        var popup = new Popup
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Left,
            HorizontalOffset = 0,
            VerticalOffset = 0,
            AllowsTransparency = true,
            StaysOpen = true,
            PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade,
            Child = CreateMenuPopupBorder(rows, 280, Math.Max(300d, Math.Min(640d, SystemParameters.WorkArea.Height - 90d)))
        };
        _activeGroupPopup = popup;
        popup.IsOpen = true;
    }

    private Button CreateActionButton(MenuAction action, Brush? accent, bool isNested = false)
    {
        var row = new DockPanel { FlowDirection = FlowDirection.RightToLeft, LastChildFill = true };
        var icon = new TextBlock
        {
            Text = GetActionGlyph(action),
            Foreground = accent ?? new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            FontSize = 15.4,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0)
        };
        DockPanel.SetDock(icon, Dock.Right);
        row.Children.Add(icon);

        var shortcut = action.Shortcut;
        if (!string.IsNullOrWhiteSpace(shortcut))
        {
            var shortcutText = new TextBlock
            {
                Text = shortcut,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontSize = 11,
                FlowDirection = FlowDirection.LeftToRight,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(9, 0, 0, 0)
            };
            DockPanel.SetDock(shortcutText, Dock.Left);
            row.Children.Add(shortcutText);
        }

        row.Children.Add(new TextBlock
        {
            Text = action.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(5, 0, 0, 0)
        });

        var button = new Button
        {
            Style = (Style)FindResource("CascadeMenuButtonStyle"),
            Content = row,
            Tag = action,
            ToolTip = action.OwnerDomain
        };
        button.Click += MenuOptionButton_Click;
        if (!isNested)
            button.MouseEnter += ActionMenuItem_MouseEnter;
        return button;
    }

    private static string GetActionGlyph(MenuAction action)
    {
        if (action.Id.StartsWith("TRE-", StringComparison.Ordinal))
            return action.Title.StartsWith("لیست", StringComparison.Ordinal) ? "▤" : "▣";
        if (action.Title.Contains("جست", StringComparison.Ordinal)) return "⌕";
        if (action.Title.Contains("اصلاح", StringComparison.Ordinal) || action.Title.Contains("ویرایش", StringComparison.Ordinal)) return "✎";
        if (action.Title.Contains("چاپ", StringComparison.Ordinal)) return "▧";
        if (action.Title.Contains("گزارش", StringComparison.Ordinal) || action.Title.StartsWith("لیست", StringComparison.Ordinal)) return "▤";
        if (action.Title.Contains("ورود", StringComparison.Ordinal) || action.Title.Contains("خروج", StringComparison.Ordinal) || action.Title.Contains("حواله", StringComparison.Ordinal)) return "⇄";
        if (action.Title.Contains("ثبت", StringComparison.Ordinal) || action.Title.Contains("صدور", StringComparison.Ordinal) || action.Title.Contains("تعریف", StringComparison.Ordinal)) return "＋";
        return "•";
    }

    private Border CreateMenuPopupBorder(UIElement content, double width, double maxHeight)
    {
        var scrollViewer = new ScrollViewer
        {
            Content = content,
            MaxHeight = maxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = true
        };
        var border = new Border
        {
            Width = width,
            MaxHeight = maxHeight,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(3),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 13,
                ShadowDepth = 3,
                Opacity = 0.18
            },
            Child = scrollViewer
        };
        border.MouseEnter += MenuPopupContent_MouseEnter;
        border.MouseLeave += MenuPopupContent_MouseLeave;
        return border;
    }

    private static Border CreateMenuSeparator() => new()
    {
        Height = 1,
        Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
        Margin = new Thickness(4, 3, 4, 3)
    };

    private void ActionMenuItem_MouseEnter(object sender, MouseEventArgs e)
    {
        _menuCloseTimer.Stop();
        // Moving onto a direct action closes the previously open sibling flyout.
        CloseGroupMenu();
    }

    private void MenuPopupContent_MouseEnter(object sender, MouseEventArgs e) => _menuCloseTimer.Stop();

    private void MenuPopupContent_MouseLeave(object sender, MouseEventArgs e)
    {
        _menuCloseTimer.Stop();
        _menuCloseTimer.Start();
    }

    // The close delay lets the pointer cross from a root menu row into the adjacent flyout.
    private void HeaderRoot_MouseLeave(object sender, MouseEventArgs e)
    {
        _menuCloseTimer.Stop();
        _menuCloseTimer.Start();
    }

    private void NavigationHost_MouseLeave(object sender, MouseEventArgs e)
    {
        _menuCloseTimer.Stop();
        _menuCloseTimer.Start();
    }

    private void MenuCloseTimer_Tick(object? sender, EventArgs e)
    {
        _menuCloseTimer.Stop();
        if (_activeMenuAnchor?.IsMouseOver == true ||
            RootMenuPopup.Child?.IsMouseOver == true ||
            _activeGroupPopup?.Child?.IsMouseOver == true)
            return;

        CloseActiveMenu();
    }

    private void CloseGroupMenu()
    {
        if (_activeGroupPopup is null)
            return;

        _activeGroupPopup.IsOpen = false;
        _activeGroupPopup.Child = null;
        _activeGroupPopup = null;
    }

    private void CloseActiveMenu()
    {
        CloseGroupMenu();
        RootMenuPopup.IsOpen = false;
        RootMenuPopup.Child = null;
        _activeMenu = null;
        _activeMenuAnchor = null;
    }

    private void MenuOptionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not MenuAction option)
            return;

        if (string.Equals(option.Id, "BASE-P-06", StringComparison.Ordinal))
        {
            ShowPersonsPreview();
            return;
        }

        // A planned destination must not replace the dashboard with a generic workspace/tab.
        // Keep the current screen visible and provide honest status feedback until that module exists.
        StatusText.Text = $"بخش «{option.Title}» هنوز پیاده‌سازی نشده است.";
        DetailsText.Text = $"مالک ماژول: {option.OwnerDomain}؛ انتخاب منو صفحه یا تب جدیدی باز نمی‌کند.";
        CloseActiveMenu();
    }

    private void QuickActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var title = button.Content switch
        {
            StackPanel stack when stack.Children.OfType<TextBlock>().FirstOrDefault() is { } text => text.Text,
            string content => content,
            _ => button.Tag?.ToString() ?? "عملیات سریع"
        };
        StatusText.Text = $"«{title}» در این پیش‌نمایش فقط نمایشی است.";
        DetailsText.Text = "این عملیات هنوز صفحه یا داده عملیاتی ندارد.";
    }

    private void ShowPersonsPreview()
    {
        _menuCloseTimer.Stop();
        CloseActiveMenu();
        DashboardView.Visibility = Visibility.Collapsed;
        PersonsPreviewView.Visibility = Visibility.Visible;
        NewPersonFormPanel.Visibility = Visibility.Collapsed;
        PreviewFormMessageText.Text = string.Empty;
        RefreshPreviewPersons();
        StatusText.Text = "پیش‌نمایش ماژول اشخاص؛ داده‌ها فقط در حافظه هستند.";
        DetailsText.Text = "هیچ نوشتنی به API یا دیتابیس انجام نمی‌شود.";
    }

    private void BackToDashboardButton_Click(object sender, RoutedEventArgs e)
    {
        PersonsPreviewView.Visibility = Visibility.Collapsed;
        NewPersonFormPanel.Visibility = Visibility.Collapsed;
        DashboardView.Visibility = Visibility.Visible;
        StatusText.Text = "داشبورد پیش‌نمایش آماده است.";
        DetailsText.Text = "داده‌های داشبورد نمونه و ساختگی هستند.";
    }

    private void SeedPreviewPersons()
    {
        _allPreviewPersons.AddRange(
        [
            new PersonPreviewRow("DEMO-0001", "شخص نمونه الف", "حقیقی", "مشتری", "ثبت نشده", "فعال · نمایشی"),
            new PersonPreviewRow("DEMO-0002", "شخص نمونه ب", "حقیقی", "مشتری، تأمین‌کننده", "ثبت نشده", "فعال · نمایشی"),
            new PersonPreviewRow("DEMO-0003", "سازمان نمونه الف", "حقوقی", "تأمین‌کننده", "ثبت نشده", "فعال · نمایشی"),
            new PersonPreviewRow("DEMO-0004", "شخص نمونه ج", "حقیقی", "بدون نقش", "ثبت نشده", "غیرفعال · نمایشی")
        ]);
    }

    private void SeedReceptionRows()
    {
        // These are synthetic UI-only fixtures. Contact values are masked and are not business data.
        _allReceptionRows.AddRange(
        [
            new ReceptionRow("REC-1012", "مشتری نمونه الف", "Samsung A54", "تعویض ال‌سی‌دی اورجینال روی کاری", "مهندس رضایی", "عادی", "امروز · ساعت ۱۶:۰۰", "۱,۲۵۰,۰۰۰ تومان", "در حال بررسی", "tech", true, "۰۹۱۲•••۱۲۳۳", true),
            new ReceptionRow("REC-1009", "مشتری نمونه ب", "Samsung S23 Ultra", "تعویض گلس پشت و شیشه دوربین", "مهندس کاظمی", "عادی", "امروز · ساعت ۱۶:۰۰", "۲,۲۰۰,۰۰۰ تومان", "در حال بررسی", "tech", false, "۰۹۱۲•••۴۵۶۷", true),
            new ReceptionRow("REC-1011", "همکار نمونه الف", "Samsung S23 Ultra", "سرویس آنتن و رفع نویز شبکه", "مهندس رضایی", "همکار", "امروز · ساعت ۱۷:۳۰", "۳,۸۰۰,۰۰۰ تومان", "منتظر قطعه", "partner", false, "۰۹۳۵•••۸۹۰۰", true),
            new ReceptionRow("REC-1010", "مشتری نمونه پ", "Xiaomi 13T Pro", "تعویض باتری و فلت شارژ", "مهندس حسینی", "عادی", "امروز · ساعت ۱۹:۰۰", "۱,۶۵۰,۰۰۰ تومان", "آماده تحویل", "tech", false, "۰۹۱۹•••۴۵۵۶", true),
            new ReceptionRow("REC-1007", "مشتری نمونه ت", "Samsung A54", "تعمیر برد و رفع خاموشی", "مهندس کاظمی", "عادی", "امروز · ساعت ۱۷:۰۰", "۵۵۰,۰۰۰ تومان", "آماده تحویل", "tech", false, "۰۹۳۰•••۱۴۴۷", true),
            new ReceptionRow("REC-1008", "همکار نمونه ب", "iPhone 12", "بررسی برد و شارژ", "مهندس حسینی", "همکار", "فردا · ساعت ۱۵:۰۰", "۲,۲۰۰,۰۰۰ تومان", "منتظر قطعه", "partner", false, "۰۹۱۰•••۷۷۸۸", false),
            new ReceptionRow("REC-1006", "مشتری نمونه ث", "Galaxy S21", "تعمیر مسیر تصویر", "مهندس رضایی", "عادی", "فردا · ساعت ۱۲:۰۰", "۲,۴۰۰,۰۰۰ تومان", "در حال بررسی", "tech", false, "۰۹۱۳•••۰۲۱۴", false),
            new ReceptionRow("REC-1005", "همکار نمونه پ", "iPhone 11", "تعویض سوکت شارژ", "مهندس کاظمی", "همکار", "فردا · ساعت ۱۱:۰۰", "۹۵۰,۰۰۰ تومان", "آماده تحویل", "partner", false, "۰۹۳۶•••۱۰۲۰", false),
            new ReceptionRow("REC-1004", "مشتری نمونه ج", "Nokia G21", "رفع مشکل صدا", "مهندس حسینی", "عادی", "امروز · ساعت ۲۰:۰۰", "۵۵۰,۰۰۰ تومان", "آماده تحویل", "other", false, "۰۹۰۰•••۰۰۰۴", true),
            new ReceptionRow("REC-1003", "مشتری نمونه چ", "Galaxy A14", "تعویض شیشه دوربین", "مهندس رضایی", "عادی", "فردا · ساعت ۱۶:۰۰", "۷۵۰,۰۰۰ تومان", "تحویل داده شده", "other", false, "۰۹۰۰•••۰۰۰۳", false),
            new ReceptionRow("REC-1002", "مشتری نمونه ح", "Huawei Nova 9", "بررسی مصرف باتری", "مهندس حسینی", "عادی", "پس‌فردا", "۶۰۰,۰۰۰ تومان", "در حال بررسی", "other", false, "۰۹۰۰•••۰۰۰۲", false),
            new ReceptionRow("REC-1001", "مشتری نمونه خ", "Galaxy A52", "رفع ایراد میکروفون", "مهندس کاظمی", "عادی", "امروز · ساعت ۲۰:۳۰", "۸۰۰,۰۰۰ تومان", "آماده تحویل", "tech", false, "۰۹۰۰•••۰۰۰۱", false)
        ]);
    }


    private void SeedDashboardStatistics()
    {
        // Synthetic chart fixtures matching the category density of the HTML reference.
        var parts = new (string Label, int Count)[]
        {
            ("ال‌سی‌دی", 48), ("باتری", 39), ("سوکت", 32), ("فلت", 28),
            ("گلس", 24), ("آی‌سی", 19), ("اسپیکر", 15), ("دوربین", 12),
            ("فریم", 9), ("آنتنا", 7)
        };
        var repairs = new (string Label, int Count)[]
        {
            ("LCD", 52), ("باتری", 41), ("سوکت شارژ", 35), ("نرم‌افزاری", 31),
            ("تعویض گلس", 27), ("آب‌خوردگی", 22), ("ریبال", 18), ("سواپ آی‌سی", 14),
            ("اسپیکر", 11), ("دوربین", 8)
        };

        var partPalette = new[]
        {
            CreateFrozenBrush("#3B82F6"), CreateFrozenBrush("#10B981"), CreateFrozenBrush("#F59E0B"),
            CreateFrozenBrush("#F43F5E"), CreateFrozenBrush("#A855F7"), CreateFrozenBrush("#06B6D4"),
            CreateFrozenBrush("#4F46E5"), CreateFrozenBrush("#14B8A6"), CreateFrozenBrush("#F97316"),
            CreateFrozenBrush("#94A3B8")
        };
        var repairPalette = new[]
        {
            CreateFrozenBrush("#3B82F6"), CreateFrozenBrush("#10B981"), CreateFrozenBrush("#F59E0B"),
            CreateFrozenBrush("#F97316"), CreateFrozenBrush("#06B6D4"), CreateFrozenBrush("#8B5CF6"),
            CreateFrozenBrush("#6366F1"), CreateFrozenBrush("#14B8A6"), CreateFrozenBrush("#EC4899"),
            CreateFrozenBrush("#94A3B8")
        };

        var maxPart = parts.Max(x => x.Count);
        for (var index = 0; index < parts.Length; index++)
        {
            var item = parts[index];
            var percent = item.Count * 100d / maxPart;
            PartStatistics.Add(new StatisticBarRow(item.Label, ToPersianDigits(item.Count), percent,
                Math.Max(18d, item.Count * 140d / maxPart), partPalette[index]));
        }

        var maxRepair = repairs.Max(x => x.Count);
        for (var index = 0; index < repairs.Length; index++)
        {
            var item = repairs[index];
            var percent = item.Count * 100d / maxRepair;
            RepairStatistics.Add(new StatisticBarRow(item.Label, ToPersianDigits(item.Count), percent,
                Math.Max(18d, item.Count * 140d / maxRepair), repairPalette[index]));
        }
    }

    private static SolidColorBrush CreateFrozenBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);
        brush.Freeze();
        return brush;
    }

    private static string ToPersianDigits(int value)
    {
        const string western = "0123456789";
        const string persian = "۰۱۲۳۴۵۶۷۸۹";
        return new string(value.ToString(CultureInfo.InvariantCulture).Select(ch =>
        {
            var index = western.IndexOf(ch);
            return index >= 0 ? persian[index] : ch;
        }).ToArray());
    }

    private void DashboardView_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyDashboardResponsiveLayout();

    private void ApplyDashboardResponsiveLayout()
    {
        if (DashboardView is null || DashboardContentPanel is null)
            return;

        var viewportWidth = DashboardView.ViewportWidth > 0 ? DashboardView.ViewportWidth : DashboardView.ActualWidth;
        var viewportHeight = DashboardView.ViewportHeight > 0 ? DashboardView.ViewportHeight : DashboardView.ActualHeight;
        if (viewportWidth > 0 && viewportHeight > 0)
        {
            // Keep a deliberate 10% breathing area on every side of the dashboard content.
            // The page may scroll; the reception table itself never scrolls vertically.
            var horizontalInset = viewportWidth * 0.10;
            var verticalInset = viewportHeight * 0.10;
            DashboardContentPanel.Margin = new Thickness(horizontalInset, verticalInset, horizontalInset, verticalInset);
        }

        UpdateReceptionGridHeight(ReceptionRows.Count);
    }

    private void UpdateReceptionGridHeight(int visibleRowCount)
    {
        if (ReceptionGrid is null)
            return;

        const double columnHeaderHeight = 48d;
        const double receptionRowHeight = 49d;
        ReceptionGrid.RowHeight = receptionRowHeight;
        // All filtered records are visible together; there is no nested vertical scrollbar.
        ReceptionGrid.Height = columnHeaderHeight + Math.Max(0, visibleRowCount) * receptionRowHeight;
    }

    private void ReceptionTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string filter)
            return;

        _activeReceptionFilter = filter;
        foreach (var tab in new[] { TabDeliveryToday, TabIntakeToday, TabTech, TabPartner, TabAll })
        {
            tab.Background = Brushes.Transparent;
            tab.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            tab.FontWeight = FontWeights.Medium;
        }
        button.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
        button.Foreground = new SolidColorBrush(Color.FromRgb(29, 78, 216));
        button.FontWeight = FontWeights.SemiBold;
        RefreshReceptionRows();
    }

    private void ReceptionSearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshReceptionRows();

    private void ReceptionStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshReceptionRows();

    private void SortByPriorityButton_Click(object sender, RoutedEventArgs e)
    {
        _allReceptionRows.Sort((left, right) => right.IsUrgent.CompareTo(left.IsUrgent));
        RefreshReceptionRows();
        StatusText.Text = "فهرست نمونه بر اساس اولویت نمایشی مرتب شد.";
    }

    private void UpdateReceptionTabCounts()
    {
        // Counts are display fixtures and are computed from the same in-memory sample rows as the worklist.
        if (TabDeliveryTodayCount is not null)
            TabDeliveryTodayCount.Text = ToPersianDigits(_allReceptionRows.Count(x => x.DueDate.Contains("امروز", StringComparison.Ordinal) && x.Status != "تحویل داده شده"));
        if (TabIntakeTodayCount is not null)
            TabIntakeTodayCount.Text = ToPersianDigits(_allReceptionRows.Count(x => x.IsReceivedToday));
        if (TabTechCount is not null)
            TabTechCount.Text = ToPersianDigits(_allReceptionRows.Count(x => x.Category == "tech"));
        if (TabPartnerCount is not null)
            TabPartnerCount.Text = ToPersianDigits(_allReceptionRows.Count(x => x.CustomerType == "همکار"));
        if (TabAllCount is not null)
            TabAllCount.Text = ToPersianDigits(_allReceptionRows.Count);
    }

    private void RefreshReceptionRows()
    {
        if (ReceptionGrid is null || ReceptionSearchBox is null || ReceptionStatusFilter is null)
            return;

        var query = ReceptionSearchBox.Text?.Trim() ?? string.Empty;
        var status = (ReceptionStatusFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
        IEnumerable<ReceptionRow> rows = _allReceptionRows;

        rows = _activeReceptionFilter switch
        {
            "deliveryToday" => rows.Where(x => x.DueDate.Contains("امروز", StringComparison.Ordinal) && x.Status != "تحویل داده شده"),
            "intakeToday" => rows.Where(x => x.IsReceivedToday),
            "tech" => rows.Where(x => x.Category == "tech"),
            "partner" => rows.Where(x => x.CustomerType == "همکار"),
            _ => rows
        };

        if (!string.Equals(status, "all", StringComparison.Ordinal))
            rows = rows.Where(x => string.Equals(x.Status, status, StringComparison.Ordinal));

        if (query.Length > 0)
        {
            rows = rows.Where(x => x.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   x.Customer.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   x.CustomerPhone.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   x.Device.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   x.Problem.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   x.Technician.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   x.Status.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var result = rows.ToList();
        UpdateReceptionTabCounts();
        ReceptionRows.Clear();
        foreach (var row in result)
            ReceptionRows.Add(row);

        UpdateReceptionGridHeight(result.Count);
    }

    private void PersonsSearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshPreviewPersons();

    private void RefreshPreviewPersons()
    {
        if (PersonsPreviewGrid is null)
            return;

        var query = PersonsSearchBox?.Text?.Trim() ?? string.Empty;
        var rows = string.IsNullOrEmpty(query)
            ? _allPreviewPersons
            : _allPreviewPersons.Where(person =>
                person.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                person.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                person.PersonType.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                person.Roles.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                person.ContactLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                person.StatusLabel.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        PreviewPersons.Clear();
        foreach (var person in rows)
            PreviewPersons.Add(person);

        UpdatePersonsGridHeight(PreviewPersons.Count);
        if (PersonsPreviewCountText is not null)
            PersonsPreviewCountText.Text = $"{_allPreviewPersons.Count} رکورد نمایشی";
        if (PersonsSearchResultText is not null)
            PersonsSearchResultText.Text = $"{PreviewPersons.Count} مورد از {_allPreviewPersons.Count} مورد نمایشی";
    }


    private void UpdatePersonsGridHeight(int visibleRowCount)
    {
        if (PersonsPreviewGrid is null)
            return;

        const double headerHeight = 46d;
        const double personRowHeight = 44d;
        PersonsPreviewGrid.RowHeight = personRowHeight;
        PersonsPreviewGrid.Height = headerHeight + Math.Max(0, visibleRowCount) * personRowHeight;
    }

    private void NewPersonButton_Click(object sender, RoutedEventArgs e)
    {
        NewPersonFormPanel.Visibility = Visibility.Visible;
        NewPersonNameBox.Text = string.Empty;
        NewPersonTypeBox.SelectedIndex = 0;
        NewPersonRoleBox.SelectedIndex = 0;
        PreviewFormMessageText.Text = string.Empty;
        NewPersonNameBox.Focus();
    }

    private void SavePreviewPersonButton_Click(object sender, RoutedEventArgs e)
    {
        var displayName = NewPersonNameBox.Text.Trim();
        if (displayName.Length == 0)
        {
            PreviewFormMessageText.Text = "نام نمایشی برای این پیش‌نمایش لازم است.";
            NewPersonNameBox.Focus();
            return;
        }

        var personType = (NewPersonTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "حقیقی";
        var role = (NewPersonRoleBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "بدون نقش";
        var row = new PersonPreviewRow(
            $"DEMO-{_nextPreviewNumber++:0000}", displayName, personType, role,
            "ثبت نشده", "موقت · فقط نمایشی");

        _allPreviewPersons.Insert(0, row);
        NewPersonFormPanel.Visibility = Visibility.Collapsed;
        PersonsSearchBox.Text = string.Empty;
        RefreshPreviewPersons();
        PersonsSearchResultText.Text = "رکورد فقط به حافظه همین اجرا افزوده شد؛ در دیتابیس ذخیره نشده است.";
    }

    private void CancelNewPersonButton_Click(object sender, RoutedEventArgs e)
    {
        NewPersonFormPanel.Visibility = Visibility.Collapsed;
        PreviewFormMessageText.Text = string.Empty;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && RootMenuPopup.IsOpen)
        {
            CloseActiveMenu();
            e.Handled = true;
        }
    }

    private void UpdatePersianClock()
    {
        var now = DateTime.Now;
        var calendar = new PersianCalendar();
        var culture = CultureInfo.GetCultureInfo("fa-IR");
        var dayName = culture.DateTimeFormat.GetDayName(now.DayOfWeek);
        var months = new[] { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
        LiveDateTimeText.Text = $"{dayName} {calendar.GetDayOfMonth(now)} {months[calendar.GetMonth(now) - 1]} {calendar.GetYear(now)} — {now:HH:mm}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _menuCloseTimer.Stop();
        _clockTimer.Stop();
        CloseActiveMenu();
        base.OnClosed(e);
    }

    public sealed record PersonPreviewRow(
        string Code,
        string DisplayName,
        string PersonType,
        string Roles,
        string ContactLabel,
        string StatusLabel);

    public sealed record ReceptionRow(
        string Code,
        string Customer,
        string Device,
        string Problem,
        string Technician,
        string CustomerType,
        string DueDate,
        string Estimate,
        string Status,
        string Category,
        bool IsUrgent,
        string CustomerPhone,
        bool IsReceivedToday)
    {
        // Explicit business-color rules, rendered in the dedicated right-edge marker column:
        // urgent = dark red; partner = purple; due today = orange; tomorrow/day-after = green.
        // Delivered rows are neutral because they are no longer pending delivery.
        public SolidColorBrush MarkerBrush => IsDelivered
            ? DefaultMarkerBrush
            : IsUrgent
                ? UrgentMarkerBrush
                : string.Equals(CustomerType, "همکار", StringComparison.Ordinal)
                    ? PartnerMarkerBrush
                    : DueDate.Contains("امروز", StringComparison.Ordinal)
                        ? TodayDeliveryMarkerBrush
                        : DueDate.Contains("فردا", StringComparison.Ordinal) || DueDate.Contains("پس‌فردا", StringComparison.Ordinal)
                            ? LaterDeliveryMarkerBrush
                            : DefaultMarkerBrush;

        public string MarkerLabel => IsDelivered
            ? "تحویل داده شده"
            : IsUrgent
                ? "تحویل فوری · قرمز پررنگ"
                : string.Equals(CustomerType, "همکار", StringComparison.Ordinal)
                    ? "ورودی همکار · بنفش"
                    : DueDate.Contains("امروز", StringComparison.Ordinal)
                        ? "موعد تحویل امروز · نارنجی"
                        : DueDate.Contains("فردا", StringComparison.Ordinal) || DueDate.Contains("پس‌فردا", StringComparison.Ordinal)
                            ? "موعد تحویل فردا یا پس‌فردا · سبز"
                            : "موعد تحویل مشخص نشده";

        public bool IsDelivered => string.Equals(Status, "تحویل داده شده", StringComparison.Ordinal);
    }

    public sealed record StatisticBarRow(string Label, string Count, double Percent, double BarHeight, SolidColorBrush BarBrush);
}
