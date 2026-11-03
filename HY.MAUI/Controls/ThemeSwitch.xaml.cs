namespace HY.MAUI.Controls;

public partial class ThemeSwitch : ContentView
{
    // 防止同步界面状态时触发主题切换
    private bool _syncingSwitch;

    // 防止初始化或响应系统主题变化时修改用户主题设置
    private bool _suppressApplyTheme;

    private Application? _subscribedApp;

    /// <summary>
    /// 是否使用深色主题。
    /// 支持双向绑定。
    /// </summary>
    public static readonly BindableProperty IsDarkProperty = BindableProperty.Create(nameof(IsDark), typeof(bool), typeof(ThemeSwitch), false, BindingMode.TwoWay, propertyChanged: OnIsDarkChanged);

    public bool IsDark
    {
        get => (bool)GetValue(IsDarkProperty);
        set => SetValue(IsDarkProperty, value);
    }

    public ThemeSwitch()
    {
        InitializeComponent();

        // 初始化时读取当前应用主题
        _suppressApplyTheme = true;

        if (Application.Current is { } app)
        {
            IsDark = app.UserAppTheme == AppTheme.Dark || (app.UserAppTheme == AppTheme.Unspecified && app.RequestedTheme == AppTheme.Dark);
        }

        _suppressApplyTheme = false;

        UpdateVisuals();

        // 监听系统主题变化
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// IsDark 发生变化时更新 UI，并应用主题。
    /// </summary>
    private static void OnIsDarkChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (ThemeSwitch)bindable;

        control.UpdateVisuals();

        if (control._suppressApplyTheme) return;

        if (Application.Current is { } app)
        {
            app.UserAppTheme = (bool)newValue ? AppTheme.Dark : AppTheme.Light;
        }
    }

    /// <summary>
    /// 用户手动切换开关。
    /// </summary>
    private void OnToggled(object? sender, ToggledEventArgs e)
    {
        if (_syncingSwitch) return;

        // 开关打开代表 Light，关闭代表 Dark
        IsDark = !e.Value;
    }

    /// <summary>
    /// 同步开关、颜色和文字透明度。
    /// </summary>
    private void UpdateVisuals()
    {
        if (DarkLabel is null || LightLabel is null || ThemeSwitchControl is null)
        {
            return;
        }

        _syncingSwitch = true;

        try
        {
            // Dark 对应开关关闭（小圆点在左侧）
            ThemeSwitchControl.IsToggled = !IsDark;

            ThemeSwitchControl.ThumbColor = IsDark
                ? Color.FromArgb("#111827")
                : Color.FromArgb("#3978F6");

            DarkLabel.Opacity = IsDark ? 1.0 : 0.5;
            LightLabel.Opacity = IsDark ? 0.5 : 1.0;
        }
        finally
        {
            _syncingSwitch = false;
        }
        //try
        //{
        //    ThemeSwitchControl.IsToggled = IsDark;

        //    ThemeSwitchControl.ThumbColor = IsDark ? Colors.MediumSpringGreen : Colors.Black;

        //    DarkLabel.Opacity = IsDark ? 1.0 : 0.5;
        //    LightLabel.Opacity = IsDark ? 0.5 : 1.0;
        //}
        //finally
        //{
        //    _syncingSwitch = false;
        //}
    }

    /// <summary>
    /// 控件加载时订阅应用主题变化事件。
    /// </summary>
    private void OnLoaded(object? sender, EventArgs e)
    {
        if (_subscribedApp is not null) return;

        if (Application.Current is not { } app) return;

        _subscribedApp = app;
        _subscribedApp.RequestedThemeChanged += OnRequestedThemeChanged;
    }

    /// <summary>
    /// 控件卸载时取消订阅，避免重复订阅。
    /// </summary>
    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_subscribedApp is null) return;

        _subscribedApp.RequestedThemeChanged -= OnRequestedThemeChanged;
        _subscribedApp = null;
    }

    /// <summary>
    /// 响应系统主题变化。
    /// 当应用仍使用系统默认主题时，同步控件状态。
    /// </summary>
    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (Application.Current is not { } app) return;

        var isDark = app.UserAppTheme == AppTheme.Dark || (app.UserAppTheme == AppTheme.Unspecified && e.RequestedTheme == AppTheme.Dark);

        _suppressApplyTheme = true;

        try
        {
            IsDark = isDark;
        }
        finally
        {
            _suppressApplyTheme = false;
        }
    }
}
