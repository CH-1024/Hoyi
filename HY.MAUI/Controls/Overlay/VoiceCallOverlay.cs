using HY.MAUI.Services;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Data;
using System.Linq;
using Font = Microsoft.Maui.Graphics.Font;

namespace HY.MAUI.Controls;

public sealed class VoiceCallOverlay : BaseOverlay
{
    private readonly CallService _callService;
    private readonly VoiceCallElement _element;
    private readonly IDispatcherTimer _timer;

    private bool _timerRunning;

    public VoiceCallOverlay(Window window, CallService callService) : base(window)
    {
        _callService = callService;
        _element = new VoiceCallElement(this, callService);

        // 视频悬浮窗支持拖动和点击。
        AddOverlayElement(_element, OverlayInteraction.TapAndDrag);

        ElementTapped += OnElementTapped;
        _callService.StateChanged += OnCallStateChanged;

        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.IsRepeating = true;
        _timer.Tick += OnTimerTick;

        UpdateState();
    }

    private async void OnElementTapped(object? sender, OverlayTappedEventArgs e)
    {
        if (ReferenceEquals(e.Element, _element))
        {
            try
            {
                await _callService.RestoreAsync();
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"恢复通话失败：{ex}");
            }
        }
    }

    public override bool Deinitialize()
    {
        ElementTapped -= OnElementTapped;

        return base.Deinitialize();
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


    private sealed class VoiceCallElement : IOverlayElement, IMovableOverlayElement
    {
        private const float DefaultWidth = 120f;
        private const float DefaultHeight = 50f;

        private readonly VoiceCallOverlay _owner;
        private readonly CallService _callService;

        public RectF Bounds { get; private set; }

        public float X { get; set; }
        public float Y { get; set; } = DeviceInfo.Platform == DevicePlatform.WinUI ? 30f : 35f;

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

            return Bounds.Contains(point);
        }

        public void MoveTo(float x, float y)
        {
            X = x;
            Y = y;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (!IsVisible)
            {
                Bounds = RectF.Zero;
                return;
            }

            Bounds = new RectF(X, Y, Width, Height);

            canvas.SaveState();

            // 悬浮窗背景
            canvas.FillColor = Color.FromArgb("#252936");
            canvas.FillRoundedRectangle(Bounds, 10f);

            // 通话状态圆点
            canvas.FillColor = Color.FromArgb("#35D39A");
            canvas.FillEllipse(X + 17f, Y + 20f, 12f, 12f);

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
