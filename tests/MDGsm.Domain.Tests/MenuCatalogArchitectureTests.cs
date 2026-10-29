using System.Text.Json;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class MenuCatalogArchitectureTests
{
    [Fact]
    public void Menu_catalog_has_eleven_roots_and_unique_nonempty_ids_and_owners()
    {
        var root = FindSolutionRoot();
        var path = Path.Combine(root, "src", "MDGsm.Desktop", "Assets", "menu-catalog-fa.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var mainMenus = document.RootElement.GetProperty("mainMenus").EnumerateArray().ToArray();
        Assert.Equal(11, mainMenus.Length);

        var expectedRootTitles = new[]
        {
            "خرید و فروش", "دریافت و پرداخت", "انبارداری", "حسابداری", "حقوق و دستمزد",
            "سامانه\u200cهای مالیاتی", "پخش مویرگی", "اطلاعات پایه", "امکانات", "تنظیمات", "راهنما"
        };
        Assert.Equal(expectedRootTitles, mainMenus.Select(menu => RequiredString(menu, "title")).ToArray());

        var ids = new List<string>();
        foreach (var menu in mainMenus)
        {
            ids.Add(RequiredString(menu, "id"));
            _ = RequiredString(menu, "title");
            var groups = menu.GetProperty("groups").EnumerateArray().ToArray();
            Assert.NotEmpty(groups);
            foreach (var group in groups)
            {
                _ = RequiredString(group, "title");
                var displayMode = RequiredString(group, "displayMode");
                Assert.Contains(displayMode, new[] { "direct", "submenu", "hidden" });
                Assert.True(group.TryGetProperty("separatorBefore", out _), "Every menu group must explicitly declare separator behavior.");
                foreach (var item in group.GetProperty("items").EnumerateArray())
                {
                    ids.Add(RequiredString(item, "id"));
                    _ = RequiredString(item, "title");
                    _ = RequiredString(item, "ownerDomain");
                    Assert.True(item.TryGetProperty("shortcut", out _), "Every action must declare shortcut metadata (possibly empty).");
                    Assert.True(item.TryGetProperty("separatorBefore", out _), "Every action must declare its separator behavior.");
                }
            }
        }

        var observedMenuItems = mainMenus
            .SelectMany(menu => menu.GetProperty("groups").EnumerateArray())
            .SelectMany(group => group.GetProperty("items").EnumerateArray())
            .ToArray();
        Assert.Equal(187, observedMenuItems.Length);

        foreach (var action in document.RootElement.GetProperty("quickActions").EnumerateArray())
        {
            ids.Add(RequiredString(action, "id"));
            _ = RequiredString(action, "title");
            _ = RequiredString(action, "ownerDomain");
        }

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Parsian_screenshot_menu_hierarchy_is_preserved_as_direct_rows_and_real_flyouts()
    {
        var root = FindSolutionRoot();
        var path = Path.Combine(root, "src", "MDGsm.Desktop", "Assets", "menu-catalog-fa.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var menus = document.RootElement.GetProperty("mainMenus").EnumerateArray().ToArray();

        JsonElement Menu(string id) => menus.Single(menu => RequiredString(menu, "id") == id);
        JsonElement Group(JsonElement menu, string title) => menu.GetProperty("groups").EnumerateArray()
            .Single(group => RequiredString(group, "title") == title);
        string Mode(JsonElement group) => RequiredString(group, "displayMode");
        string[] Titles(JsonElement group) => group.GetProperty("items").EnumerateArray()
            .Select(item => RequiredString(item, "title")).ToArray();

        var sales = Menu("sales");
        Assert.Equal("direct", Mode(Group(sales, "اسناد و گزارش‌ها")));
        Assert.Equal("submenu", Mode(Group(sales, "سایر گزارشات")));
        Assert.Equal("لیست فاکتورها", Titles(Group(sales, "اسناد و گزارش‌ها"))[7]);

        var treasury = Menu("treasury");
        Assert.Equal("direct", Mode(Group(treasury, "رسیدها")));
        Assert.Equal("submenu", Mode(Group(treasury, "عملیات چک‌ها")));
        var checkActions = Titles(Group(treasury, "عملیات چک‌ها"));
        Assert.Equal(10, checkActions.Length);
        Assert.Contains("عودت چک‌ها در جریان وصول", checkActions);
        Assert.Contains("پاس چک پرداختنی", checkActions);

        var checkReports = Group(treasury, "گزارش چک‌ها");
        var checkReportTitles = Titles(checkReports);
        Assert.Equal(16, checkReportTitles.Length);
        Assert.Equal("لیست چک‌های دریافتی عودت‌شده", checkReportTitles[6]);
        var checkReportItems = checkReports.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("Ctrl+W", RequiredString(checkReportItems[1], "shortcut"));
        Assert.Equal("Shift+P", RequiredString(checkReportItems[9], "shortcut"));
        Assert.Equal("Ctrl+F13", RequiredString(checkReportItems[15], "shortcut"));
        Assert.Contains("CHK-Q-01", treasury.GetProperty("groups").EnumerateArray()
            .SelectMany(group => group.GetProperty("items").EnumerateArray()).Select(item => RequiredString(item, "id")));

        var inventory = Menu("inventory");
        Assert.Equal("submenu", Mode(Group(inventory, "ورود/خروج به انبار")));
        Assert.Equal("direct", Mode(Group(inventory, "جست‌وجو و گزارش موجودی")));
        Assert.Equal("submenu", Mode(Group(inventory, "سایر گزارشات موجودی")));
        Assert.Equal("موجودی کالا در سطح‌ها", Titles(Group(inventory, "سایر گزارشات موجودی"))[1]);

        var accounting = Menu("accounting");
        Assert.Equal("direct", Mode(Group(accounting, "اسناد، حساب‌ها و گزارش‌های مستقیم")));
        Assert.Equal("submenu", Mode(Group(accounting, "تراز آزمایشی")));
        Assert.Equal("submenu", Mode(Group(accounting, "دفاتر حسابداری")));

        var payroll = Menu("payroll");
        Assert.Equal("direct", Mode(Group(payroll, "عملیات حقوق و دستمزد")));
        Assert.Equal("submenu", Mode(Group(payroll, "تنظیمات")));
        Assert.Contains("حوزه‌های مالیاتی", Titles(Group(payroll, "تنظیمات")));

        var baseData = Menu("base-data");
        Assert.Equal("direct", Mode(Group(baseData, "تعریف‌ها")));
        var baseDefinitions = Group(baseData, "تعریف‌ها").GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("Ctrl+C", RequiredString(baseDefinitions[0], "shortcut"));
        Assert.True(baseDefinitions[5].GetProperty("separatorBefore").GetBoolean());
        Assert.Equal("direct", Mode(Group(baseData, "ثبت و فهرست اشخاص")));
        Assert.Equal("hidden", Mode(Group(baseData, "پیشنهادهای آینده؛ فعلاً پنهان")));

        var utilities = Menu("utilities");
        Assert.Equal("submenu", Mode(Group(utilities, "نمایش تاریخچه اصلاح و حذف")));
        var settings = Menu("settings");
        Assert.Equal("direct", Mode(Group(settings, "تنظیمات اصلی")));
        Assert.Equal("hidden", Mode(Group(settings, "گزینه‌های تکمیلی HTML؛ فعلاً پنهان")));
        Assert.All(Menu("tax").GetProperty("groups").EnumerateArray(), group => Assert.Equal("hidden", Mode(group)));
        Assert.All(Menu("help").GetProperty("groups").EnumerateArray(), group => Assert.Equal("hidden", Mode(group)));

        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "MDGsm.Desktop", "MainWindow.xaml.cs"));
        Assert.Contains("var hasEvidenceBackedContent = root.Groups.Any(group =>", mainWindow);
        Assert.Contains("if (!hasEvidenceBackedContent)", mainWindow);
    }

    [Fact]
    public void Data_ownership_registry_has_unique_concepts_and_explicit_owners()
    {
        var root = FindSolutionRoot();
        var path = Path.Combine(root, "docs", "architecture", "data-ownership-registry.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var concepts = document.RootElement.GetProperty("concepts").EnumerateArray().ToArray();
        Assert.True(concepts.Length >= 25, $"Expected a useful shared-concept registry; found {concepts.Length} concepts.");

        var ids = new List<string>();
        var names = new List<string>();
        foreach (var concept in concepts)
        {
            ids.Add(RequiredString(concept, "id"));
            names.Add(RequiredString(concept, "canonicalConcept"));
            _ = RequiredString(concept, "ownerDomain");
            _ = RequiredString(concept, "mustNotCreate");
        }

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        Assert.True(element.TryGetProperty(propertyName, out var value), $"Missing required property '{propertyName}'.");
        var result = value.GetString();
        Assert.False(string.IsNullOrWhiteSpace(result), $"Property '{propertyName}' must not be empty.");
        return result!;
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate MDGsm.sln from test output directory.");
    }
}
