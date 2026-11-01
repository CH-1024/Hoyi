using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Controls;

namespace HY.MAUI.Controls;

public partial class BottomTabBar : ContentView
{
    private Shell? _shell;

    private string _selectedTab = string.Empty;

    public BottomTabBar()
    {
        InitializeComponent();

        // 初始化导航栏背景主题
        ApplySurfaceTheme();

        // 默认选中聊天
        SetSelectedTab("chat");

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }


    #region Shell 导航监听

    private void OnLoaded(object? sender, EventArgs e)
    {
        AttachShell();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_shell != null)
        {
            _shell.Navigated -= Shell_Navigated;
            _shell = null;
        }
    }

    private void AttachShell()
    {
        var shell = Shell.Current;

        if (shell == null || ReferenceEquals(shell, _shell)) return;

        if (_shell != null)
        {
            _shell.Navigated -= Shell_Navigated;
        }

        _shell = shell;

        // 监听页面切换
        _shell.Navigated += Shell_Navigated;

        // 同步当前页面
        UpdateFromLocation(_shell.CurrentState.Location.OriginalString);
    }

    private void Shell_Navigated(object? sender, ShellNavigatedEventArgs e)
    {
        UpdateFromLocation(e.Current.Location.OriginalString);
    }

    private void UpdateFromLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return;

        var segments = location.Split('?', '#')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);

        var route = segments.LastOrDefault(segment =>
            segment.Equals("chat", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("contact", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("mine", StringComparison.OrdinalIgnoreCase));

        if (route != null)
        {
            SetSelectedTab(route.ToLowerInvariant(), true);
        }
    }

    #endregion


    #region 主题颜色

    private void ApplySurfaceTheme()
    {
        // 主体背景
        BarSurface.SetAppThemeColor(Border.BackgroundProperty, Color.FromArgb("#FFFFFF"), Color.FromArgb("#202127"));

        // 主体边框
        BarSurface.SetAppThemeColor( Border.StrokeProperty, Color.FromArgb("#EAEDF3"), Color.FromArgb("#383A44"));
    }

    private static void UpdateTabStyle(Border border, Image icon, Label label, bool selected)
    {
        border.SetAppThemeColor(
            Border.BackgroundProperty,
            selected
                ? Color.FromArgb("#EEF3FF")
                : Colors.Transparent,
            selected
                ? Color.FromArgb("#293B59")
                : Colors.Transparent);

        border.SetAppThemeColor(
            Border.StrokeProperty,
            selected
                ? Color.FromArgb("#DCE7FF")
                : Colors.Transparent,
            selected
                ? Color.FromArgb("#405B83")
                : Colors.Transparent);

        label.SetAppThemeColor(
            Label.TextColorProperty,
            selected
                ? Color.FromArgb("#3568ED")
                : Color.FromArgb("#7B8494"),
            selected
                ? Color.FromArgb("#91B7FF")
                : Color.FromArgb("#AEB3C0"));

        label.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;

        icon.Opacity = selected ? 1 : 0.68;
    }

    #endregion


    #region 选中状态

    private void SetSelectedTab(string tab, bool animate = false)
    {
        if (_selectedTab == tab) return;

        _selectedTab = tab;

        // 更新三个选项卡状态
        UpdateTabStyle(ChatTab, ChatIcon, ChatLabel, tab == "chat");

        UpdateTabStyle(ContactTab, ContactIcon, ContactLabel, tab == "contact");

        UpdateTabStyle(MineTab, MineIcon, MineLabel, tab == "mine");

        // 选中动画
        if (animate)
        {
            var selectedBorder = tab switch
            {
                "chat" => ChatTab,
                "contact" => ContactTab,
                "mine" => MineTab,
                _ => null
            };

            if (selectedBorder != null)
            {
                _ = PlaySelectionAnimationAsync(selectedBorder);
            }
        }
    }

    private static async Task PlaySelectionAnimationAsync(Border border)
    {
        try
        {
            await border.ScaleToAsync(0.96, 65, Easing.CubicOut);

            await border.ScaleToAsync(1.0, 130, Easing.CubicOut);
        }
        catch (Exception)
        {
            // 页面卸载或动画中断时忽略
        }
        finally
        {
            border.Scale = 1;
        }
    }

    #endregion


    #region 页面切换

    private async Task NavigateAsync(string route)
    {
        var shell = Shell.Current;

        if (shell == null) return;

        try
        {
            await shell.GoToAsync(route);
        }
        catch (Exception ex)
        {
            //System.Diagnostics.Debug.WriteLine($"Tab navigation failed: {ex}");
        }
    }

    private async void Chat_Tapped(object? sender, TappedEventArgs e)
    {
        await NavigateAsync("//main/chat");
    }

    private async void Contact_Tapped(object? sender, TappedEventArgs e)
    {
        await NavigateAsync("//main/contact");
    }

    private async void Mine_Tapped(object? sender, TappedEventArgs e)
    {
        await NavigateAsync("//main/mine");
    }

    #endregion

}
