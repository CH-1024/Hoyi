using HY.MAUI.Services;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Data;
using System.Linq;
using Font = Microsoft.Maui.Graphics.Font;

#if WINDOWS
using Microsoft.UI.Xaml.Input;
#endif

namespace HY.MAUI.Controls;

public sealed class VoiceCallOverlay : WindowOverlay
{
    private readonly CallService _callService;
    private readonly VoiceCallElement _element;
    private readonly IDispatcherTimer _timer;

    private bool _timerRunning;

    public VoiceCallOverlay(Window window, CallService callService) : base(window)
    {
        _callService = callService;
        _element = new VoiceCallElement(this, callService);

        // 只拦截悬浮条区域的触摸
        EnableDrawableTouchHandling = true;
        DisableUITouchEventPassthrough = false;

        AddWindowElement(_element);

        Tapped += OnTapped;
        _callService.StateChanged += OnCallStateChanged;

        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.IsRepeating = true;
        _timer.Tick += OnTimerTick;

        UpdateState();
    }

#if WINDOWS

    private Microsoft.UI.Xaml.FrameworkElement? _dragSurface;

    private bool _isDragging;
    private bool _hasDragged;

    private Point _pointerStart;
    private float _elementStartX;
    private float _elementStartY;

    /// <summary>
    /// 等待 WindowOverlay 的原生绘制视图初始化完成。
    /// </summary>
    public override bool Initialize()
    {
        if (!base.Initialize())
            return false;

        AttachPointerHandlers();

        return true;
    }

    private void AttachPointerHandlers()
    {
        if (_dragSurface != null)
            return;

        _dragSurface = GraphicsView;

        if (_dragSurface == null)
            return;

        _dragSurface.PointerPressed += OnPointerPressed;
        _dragSurface.PointerMoved += OnPointerMoved;
        _dragSurface.PointerReleased += OnPointerReleased;
        _dragSurface.PointerCaptureLost += OnPointerCaptureLost;
    }

    /// <summary>
    /// 鼠标按下。
    /// </summary>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _hasDragged = false;

        if (_dragSurface == null)
            return;

        var position = e.GetCurrentPoint(_dragSurface).Position;

        var point = new Point(position.X, position.Y);

        // 只允许在悬浮窗区域开始拖动。
        if (!_element.Contains(point))
            return;

        _isDragging = true;

        _pointerStart = point;

        _elementStartX = _element.X;
        _elementStartY = _element.Y;

        // 捕获鼠标，防止拖出悬浮窗后丢失移动事件。
        _dragSurface.CapturePointer(e.Pointer);
    }

    /// <summary>
    /// 鼠标移动。
    /// </summary>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _dragSurface == null)
            return;

        var position = e.GetCurrentPoint(_dragSurface).Position;

        float dx = (float)(position.X - _pointerStart.X);
        float dy = (float)(position.Y - _pointerStart.Y);

        // 过滤轻微鼠标抖动，避免普通点击被识别成拖动。
        if (!_hasDragged && dx * dx + dy * dy < 16f)
            return;

        _hasDragged = true;

        float x = _elementStartX + dx;
        float y = _elementStartY + dy;

        UpdateElementPosition(x, y);
    }

    /// <summary>
    /// 鼠标松开。
    /// </summary>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;

        _dragSurface?.ReleasePointerCaptures();
    }

    /// <summary>
    /// 鼠标捕获丢失。
    /// </summary>
    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
    }

    /// <summary>
    /// 更新位置并限制在窗口范围内。
    /// </summary>
    private void UpdateElementPosition(float x, float y)
    {
        if (_dragSurface != null &&
            _dragSurface.ActualWidth > 0 &&
            _dragSurface.ActualHeight > 0)
        {
            float maxX = Math.Max(0, (float)_dragSurface.ActualWidth - _element.Width);

            float maxY = Math.Max(0, (float)_dragSurface.ActualHeight - _element.Height);

            x = Math.Clamp(x, 0, maxX);
            y = Math.Clamp(y, 0, maxY);
        }
        else
        {
            x = Math.Max(0, x);
            y = Math.Max(0, y);
        }

        _element.X = x;
        _element.Y = y;

        // 请求重绘。
        Invalidate();
    }

    private void DetachPointerHandlers()
    {
        if (_dragSurface == null)
            return;

        _dragSurface.PointerPressed -= OnPointerPressed;
        _dragSurface.PointerMoved -= OnPointerMoved;
        _dragSurface.PointerReleased -= OnPointerReleased;
        _dragSurface.PointerCaptureLost -= OnPointerCaptureLost;

        _dragSurface = null;
        _isDragging = false;
    }

#endif

    private async void OnTapped(object? sender, WindowOverlayTappedEventArgs e)
    {
        if (!e.WindowOverlayElements.Contains(_element))
            return;
        ;
#if WINDOWS
        // 拖动结束后不执行点击操作。
        if (_hasDragged)
        {
            _hasDragged = false;
            return;
        }
#endif

        try
        {
            await _callService.RestoreAsync();
        }
        catch (Exception ex)
        {
            //Debug.WriteLine($"恢复通话失败：{ex}");
        }
    }

    private void OnCallStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(UpdateState);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Invalidate();
    }

    private void UpdateState()
    {
        bool visible = _callService.IsActive && _callService.IsMinimized;

        if (visible && !_timerRunning)
        {
            _timer.Start();
            _timerRunning = true;
        }
        else if (!visible && _timerRunning)
        {
            _timer.Stop();
            _timerRunning = false;
        }

        Invalidate();
    }

    public override bool Deinitialize()
    {
        _callService.StateChanged -= OnCallStateChanged;
        Tapped -= OnTapped;
        _timer.Tick -= OnTimerTick;

        if (_timerRunning)
        {
            _timer.Stop();
            _timerRunning = false;
        }

#if WINDOWS
        DetachPointerHandlers();
#endif

        return base.Deinitialize();
    }

    private sealed class VoiceCallElement : IWindowOverlayElement
    {
        private readonly VoiceCallOverlay _owner;
        private readonly CallService _callService;

        private RectF _bounds;

        private const float DefaultWidth = 110f;
        private const float DefaultHeight = 50f;

        public float X { get; set; }

        public float Y { get; set; } = 50f;

        public float Width => DefaultWidth;

        public float Height => DefaultHeight;

        public VoiceCallElement(VoiceCallOverlay owner, CallService callService)
        {
            _owner = owner;
            _callService = callService;
        }

        private bool IsVisible => _callService.IsActive && _callService.IsMinimized;

        public bool Contains(Point point)
        {
            if (!IsVisible)
                return false;

            return _bounds.Contains(point);
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (!IsVisible)
            {
                _bounds = RectF.Zero;
                return;
            }

            _bounds = new RectF(X, Y, Width, Height);

            canvas.SaveState();

            // 悬浮窗背景
            canvas.FillColor = Color.FromArgb("#252936");
            canvas.FillRoundedRectangle(_bounds, 10f);

            // 通话状态圆点
            canvas.FillColor = Color.FromArgb("#35D39A");
            canvas.FillEllipse(X + 17f, Y + 23f, 12f, 12f);

            // 第一行
            canvas.Font = Font.Default;
            canvas.FontColor = Colors.White;
            canvas.FontSize = 13f;

            // 通话状态
            canvas.DrawString(
                "正在通话",
                X + 42f,
                Y + 5f,
                Width - 52f,
                21f,
                HorizontalAlignment.Left,
                VerticalAlignment.Center);

            // 第二行
            canvas.Font = Font.Default;
            canvas.FontColor = Color.FromArgb("#C6CBD5");
            canvas.FontSize = 13;

            //string name = _callService.TargetName == null ? "对方" :
            //              _callService.TargetName.Length > 10 ? _callService.TargetName.Substring(0, 10) + "..." :
            //              _callService.TargetName;
            TimeSpan elapsed = _callService.Elapsed;
            string duration = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";

            // 对方昵称和通话时长
            canvas.DrawString(
                duration,
                X + 42f,
                Y + 28f,
                Width - 52f,
                19f,
                HorizontalAlignment.Left,
                VerticalAlignment.Center);

            canvas.RestoreState();
        }
    }
}
