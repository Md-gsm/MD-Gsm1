using System.Text.Json;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class PersonsPreviewInstallerTests
{
    [Fact]
    public void Persons_preview_is_reachable_from_catalog_and_hover_menu()
    {
        var root = FindSolutionRoot();
        var catalogPath = Path.Combine(root, "src", "MDGsm.Desktop", "Assets", "menu-catalog-fa.json");
        using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var allItems = catalog.RootElement.GetProperty("mainMenus")
            .EnumerateArray()
            .SelectMany(menu => menu.GetProperty("groups").EnumerateArray())
            .SelectMany(group => group.GetProperty("items").EnumerateArray())
            .ToArray();
        Assert.Contains(allItems, item =>
            item.GetProperty("id").GetString() == "BASE-P-06" &&
            item.GetProperty("title").GetString() == "ثبت و فهرست اشخاص");

        var codeBehind = File.ReadAllText(Path.Combine(root, "src", "MDGsm.Desktop", "MainWindow.xaml.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MDGsm.Desktop", "MainWindow.xaml"));
        Assert.Contains("ShowPersonsPreview();", codeBehind);
        Assert.Contains("x:Name=\"PersonsPreviewView\"", xaml);
        Assert.Contains("MouseEnter=\"NavMenuButton_MouseEnter\"", xaml);
        Assert.Contains("MouseLeave=\"HeaderRoot_MouseLeave\"", xaml);
        Assert.DoesNotContain("Click=\"NavMenuButton_Click\"", xaml);
        Assert.Contains("ReceptionGrid", xaml);
        Assert.DoesNotContain("x:Name=\"DestinationView\"", xaml);
    }


    [Fact]
    public void Dashboard_matches_reference_sections_and_uses_hover_only_cascading_menus()
    {
        var root = FindSolutionRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MDGsm.Desktop", "MainWindow.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "src", "MDGsm.Desktop", "MainWindow.xaml.cs"));
        var catalogPath = Path.Combine(root, "src", "MDGsm.Desktop", "Assets", "menu-catalog-fa.json");
        using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));

        Assert.Contains("x:Name=\"HeaderRoot\" Grid.Row=\"0\" MouseLeave=\"HeaderRoot_MouseLeave\"", xaml);
        Assert.Contains("x:Name=\"NavigationHost\" Grid.Row=\"0\"", xaml);
        Assert.Contains("x:Name=\"RootMenuPopup\" Placement=\"Custom\"", xaml);
        Assert.Contains("MouseEnter=\"NavMenuButton_MouseEnter\"", xaml);
        Assert.DoesNotContain("Click=\"NavMenuButton_Click\"", xaml);
        Assert.DoesNotContain("MegaMenuPanel", xaml);
        Assert.Contains("Placement = PlacementMode.Custom", codeBehind);
        Assert.Contains("PlaceRootMenuPopup", codeBehind);
        Assert.Contains("Placement = PlacementMode.Left", codeBehind);
        Assert.Contains("GroupMenuItem_MouseEnter", codeBehind);
        Assert.Contains("GetActionGlyph(action)", codeBehind);
        Assert.Contains("switch (group.DisplayMode.Trim().ToLowerInvariant())", codeBehind);
        Assert.Contains("if (_activeMenuAnchor?.IsMouseOver == true", codeBehind);
        Assert.DoesNotContain("if (!HeaderRoot.IsMouseOver)", codeBehind);

        var menuCount = catalog.RootElement.GetProperty("mainMenus").GetArrayLength();
        Assert.Equal(11, menuCount);
        Assert.Contains("کل پذیرش‌های ثبت‌شده", xaml);
        Assert.Contains("ReceptionGrid", xaml);
        Assert.Contains("WindowState=\"Maximized\"", xaml);
        Assert.Contains("Loaded=\"MainWindow_Loaded\"", xaml);
        Assert.Contains("SizeChanged=\"DashboardView_SizeChanged\"", xaml);
        Assert.Contains("MinHeight=\"600\"", xaml);
        Assert.DoesNotContain("Width=\"1075\"", xaml);
        Assert.Contains("<StackPanel x:Name=\"DashboardContentPanel\" HorizontalAlignment=\"Stretch\" Margin=\"0\">", xaml); // Dashboard uses a responsive, scrollable content panel; outer page padding is intentionally not hard-coded.
        Assert.Contains("DashboardView_SizeChanged", codeBehind);
        Assert.Contains("var horizontalInset = viewportWidth * 0.10;", codeBehind);
        Assert.Contains("var verticalInset = viewportHeight * 0.10;", codeBehind);
        Assert.Contains("DashboardContentPanel.Margin = new Thickness(horizontalInset, verticalInset, horizontalInset, verticalInset);", codeBehind);
        Assert.Contains("x:Name=\"TabDeliveryToday\" Tag=\"deliveryToday\"", xaml);
        Assert.Contains("Background=\"#EFF6FF\" Foreground=\"#1D4ED8\" FontWeight=\"SemiBold\"", xaml);
        Assert.Contains("x:Name=\"TabIntakeToday\" Tag=\"intakeToday\"", xaml);
        Assert.Contains("private string _activeReceptionFilter = \"deliveryToday\";", codeBehind);
        Assert.Contains("<Border Background=\"{Binding MarkerBrush}\" ToolTip=\"{Binding MarkerLabel}\"", xaml);
        Assert.Contains("<Setter Property=\"ToolTip\" Value=\"{Binding MarkerLabel}\"/>", xaml);
        Assert.Contains("UrgentMarkerBrush = CreateFrozenBrush(\"#E00000\")", codeBehind);
        Assert.Contains("PartnerMarkerBrush = CreateFrozenBrush(\"#8B5CF6\")", codeBehind);
        Assert.Contains("LaterDeliveryMarkerBrush = CreateFrozenBrush(\"#10B981\")", codeBehind);
        Assert.Contains("TodayDeliveryMarkerBrush = CreateFrozenBrush(\"#F97316\")", codeBehind);
        Assert.Contains("DueDate.Contains(\"امروز\", StringComparison.Ordinal) && x.Status != \"تحویل داده شده\"", codeBehind);
        var rowStyleStart = xaml.IndexOf("<Style TargetType=\"DataGridRow\">", StringComparison.Ordinal);
        var rowStyleEnd = xaml.IndexOf("</Style>", rowStyleStart, StringComparison.Ordinal);
        Assert.True(rowStyleStart >= 0 && rowStyleEnd > rowStyleStart);
        var rowStyle = xaml[rowStyleStart..rowStyleEnd];
        Assert.DoesNotContain("DataTrigger Binding=\"{Binding Status}\"", rowStyle);
        Assert.Contains("FontSize=\"11\"", xaml); // readable top-menu labels
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"13\"/>", xaml); // readable table labels
        Assert.True(xaml.IndexOf("x:Name=\"TabDeliveryToday\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"TabIntakeToday\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"TabIntakeToday\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"TabTech\"", StringComparison.Ordinal));
        Assert.Contains("TabDeliveryTodayCount.Text", codeBehind);
        Assert.Contains("TabIntakeTodayCount.Text", codeBehind);
        Assert.Contains("IsReceivedToday", codeBehind);
        Assert.Contains("<ItemsPanelTemplate><UniformGrid Columns=\"10\"/></ItemsPanelTemplate>", xaml);
        Assert.Contains("مشتری", xaml);
        Assert.Contains("ورودی و تعمیرات ۴ هفته اخیر", xaml);
        Assert.Contains("وضعیت و بار کاری تکنسین‌ها", xaml);
        Assert.Contains("گزارشات آماری مصرف قطعات و انواعی از تعمیرات", xaml);
        Assert.Contains("آمار انواع تعمیرات (۱۰ مورد پرتکرار)", xaml);
        Assert.Contains("نمودار ستونی عمودی", xaml);
        Assert.Contains("Height=\"{Binding BarHeight}\"", xaml);
        Assert.Contains("Background=\"{Binding BarBrush}\"", xaml);
        Assert.Contains("CustomerPhone", xaml);
        Assert.DoesNotContain("Click=\"CheckApiButton_Click\"", xaml);
        Assert.DoesNotContain("x:Name=\"DestinationView\"", xaml);
        Assert.Contains("انتخاب منو صفحه یا تب جدیدی باز نمی‌کند", codeBehind);
        Assert.Contains("مشتری نمونه الف", codeBehind);
        Assert.Contains("بر پایه داده‌های ساختگی", xaml);
    }

    [Fact]
    public void Preview_add_handler_is_in_memory_only_and_explicitly_demonstration_scoped()
    {
        var root = FindSolutionRoot();
        var codeBehind = File.ReadAllText(Path.Combine(root, "src", "MDGsm.Desktop", "MainWindow.xaml.cs"));
        var start = codeBehind.IndexOf("private void SavePreviewPersonButton_Click", StringComparison.Ordinal);
        Assert.True(start >= 0, "Preview-only add handler must exist.");
        var end = codeBehind.IndexOf("private void CancelNewPersonButton_Click", start, StringComparison.Ordinal);
        Assert.True(end > start, "Preview-only add handler must have a clear end boundary.");
        var handler = codeBehind[start..end];

        Assert.Contains("_allPreviewPersons.Insert(0, row)", handler);
        Assert.DoesNotContain("_apiClient", handler);
        Assert.False(handler.Contains("DbContext", StringComparison.OrdinalIgnoreCase));
        Assert.False(handler.Contains("SaveChanges", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("فقط نمایشی", handler);
    }

    [Fact]
    public void Windows_installer_uses_self_contained_publish_and_does_not_call_python()
    {
        var root = FindSolutionRoot();
        var buildScript = File.ReadAllText(Path.Combine(root, "scripts", "Build-Windows-Installer.ps1"));
        var iss = File.ReadAllText(Path.Combine(root, "installer", "MDGsm-Persons-Preview.iss"));

        Assert.Contains("--runtime win-x64 --self-contained true", buildScript);
        Assert.Contains("ISCC.exe", buildScript);
        Assert.Contains("MDGsm.Desktop.exe", iss);
        Assert.False(buildScript.Contains("python.exe", StringComparison.OrdinalIgnoreCase));
        Assert.False(buildScript.Contains("python scripts", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate MDGsm.sln from test output directory.");
    }
}
