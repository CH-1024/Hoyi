#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Maui.Graphics;
#elif IOS || MACCATALYST
using UIKit;
using Microsoft.Maui.Graphics;
#elif ANDROID
using Android.Views;
using System.Diagnostics;
using System.Linq;
#endif

namespace HY.MAUI.Controls;

public abstract partial class BaseOverlay
{
#if WINDOWS

    private FrameworkElement? _windowsSurface;

    #region Windows 输入

    partial void OnAttachPlatformInput()
    {
        Tapped += OnWindowOverlayTapped;

        _windowsSurface = GraphicsView;

        if (_windowsSurface == null)
            return;

        _windowsSurface.PointerPressed += OnPointerPressed;
        _windowsSurface.PointerMoved += OnPointerMoved;
        _windowsSurface.PointerReleased += OnPointerReleased;
        _windowsSurface.PointerCaptureLost += OnPointerCaptureLost;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_windowsSurface == null)
            return;

        var position = e.GetCurrentPoint(_windowsSurface).Position;

        var point = new Point(position.X, position.Y);

        if (!BeginPointer(point))
            return;

        _windowsSurface.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_windowsSurface == null)
            return;

        var position = e.GetCurrentPoint(_windowsSurface).Position;

        MovePointer(new Point(position.X, position.Y), (float)_windowsSurface.ActualWidth, (float)_windowsSurface.ActualHeight, suppressTapOnDrag: true);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndPointer();

        _windowsSurface?.ReleasePointerCaptures();
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndPointer();
    }

    partial void OnDetachPlatformInput()
    {
        Tapped -= OnWindowOverlayTapped;

        if (_windowsSurface == null)
            return;

        _windowsSurface.PointerPressed -= OnPointerPressed;
        _windowsSurface.PointerMoved -= OnPointerMoved;
        _windowsSurface.PointerReleased -= OnPointerReleased;
        _windowsSurface.PointerCaptureLost -= OnPointerCaptureLost;

        _windowsSurface = null;

        EndPointer();
    }

    #endregion

#elif IOS || MACCATALYST

    private UIView? _appleSurface;

    private UIPanGestureRecognizer? _panGesture;
    private UITapGestureRecognizer? _tapGesture;

    private bool _previousInteractionEnabled;

    #region Apple 平台输入

    partial void OnAttachPlatformInput()
    {
        _appleSurface = GraphicsView;

        if (_appleSurface == null)
            return;

        _previousInteractionEnabled = _appleSurface.UserInteractionEnabled;

        _appleSurface.UserInteractionEnabled = true;

        // 拖动手势。
        _panGesture = new UIPanGestureRecognizer(OnPanGesture)
        {
            MinimumNumberOfTouches = 1,
            MaximumNumberOfTouches = 1,
            CancelsTouchesInView = false
        };

        // 只允许从可拖动元素开始拖动。
        _panGesture.ShouldBegin = ShouldBeginPan;

        // 点击手势。
        _tapGesture = new UITapGestureRecognizer(OnTapGesture)
        {
            NumberOfTapsRequired = 1,
            CancelsTouchesInView = false
        };

        // 拖动手势优先于点击手势。
        _tapGesture.RequireGestureRecognizerToFail(_panGesture);

        _appleSurface.AddGestureRecognizer(_panGesture);
        _appleSurface.AddGestureRecognizer(_tapGesture);
    }

    /// <summary>
    /// 判断 Pan 是否可以开始识别。
    /// </summary>
    private bool ShouldBeginPan(UIGestureRecognizer recognizer)
    {
        if (_appleSurface == null || recognizer is not UIPanGestureRecognizer pan)
            return false;

        var startPoint = GetPanStartPoint(pan);

        return FindElementAt(startPoint, OverlayInteraction.Drag) is IMovableOverlayElement;
    }

    /// <summary>
    /// 获取鼠标最初按下的位置。
    /// </summary>
    private Point GetPanStartPoint(UIPanGestureRecognizer gesture)
    {
        var location = gesture.LocationInView(_appleSurface);
        var translation = gesture.TranslationInView(_appleSurface);

        return new Point(location.X - translation.X, location.Y - translation.Y);
    }

    /// <summary>
    /// Pan 手势。
    /// </summary>
    private void OnPanGesture(UIPanGestureRecognizer gesture)
    {
        if (_appleSurface == null)
            return;

        switch (gesture.State)
        {
            case UIGestureRecognizerState.Began:
                {
                    BeginPointer(GetPanStartPoint(gesture));
                    break;
                }

            case UIGestureRecognizerState.Changed:
                {
                    var location = gesture.LocationInView(_appleSurface);

                    MovePointer(new Point(location.X, location.Y), (float)_appleSurface.Bounds.Width, (float)_appleSurface.Bounds.Height, suppressTapOnDrag: false);

                    break;
                }

            case UIGestureRecognizerState.Ended:
            case UIGestureRecognizerState.Cancelled:
            case UIGestureRecognizerState.Failed:
                {
                    EndPointer();
                    break;
                }
        }
    }

    /// <summary>
    /// Tap 手势，统一分发点击事件。
    /// </summary>
    private void OnTapGesture(UITapGestureRecognizer gesture)
    {
        if (_appleSurface == null)
            return;

        var location = gesture.LocationInView(_appleSurface);

        RaiseTapAt(new Point(location.X, location.Y));
    }

    partial void OnDetachPlatformInput()
    {
        if (_appleSurface != null)
        {
            if (_panGesture != null)
                _appleSurface.RemoveGestureRecognizer(_panGesture);

            if (_tapGesture != null)
                _appleSurface.RemoveGestureRecognizer(_tapGesture);

            _appleSurface.UserInteractionEnabled =
                _previousInteractionEnabled;
        }

        _panGesture?.Dispose();
        _tapGesture?.Dispose();

        _panGesture = null;
        _tapGesture = null;

        _appleSurface = null;

        EndPointer();
    }

    #endregion

#elif ANDROID

    #region Android 输入


    private Android.Views.View? _androidTouchView;

    private bool _androidPointerCaptured;
    private bool _androidIsDragging;

    /// <summary>
    /// 订阅 Android 原生触摸事件。
    /// 注意：此时 base.Initialize() 已经执行完成。
    /// </summary>
    partial void OnAttachPlatformInput()
    {
        _androidTouchView = GraphicsView;

        if (_androidTouchView == null)
        {
            Debug.WriteLine("[Overlay.Android] GraphicsView is null");
            return;
        }

        _androidTouchView.Touch += OnAndroidTouch;

        Debug.WriteLine(
            $"[Overlay.Android] Touch attached. " +
            $"Width={_androidTouchView.Width}, " +
            $"Height={_androidTouchView.Height}, " +
            $"Density={Density}");
    }

    /// <summary>
    /// Android 原生触摸处理。
    /// </summary>
    private void OnAndroidTouch(
        object? sender,
        Android.Views.View.TouchEventArgs e)
    {
        var motion = e.Event;

        if (motion == null)
            return;

        float density = Density > 0 ? Density : 1f;

        // 重要：转换成 MAUI 使用的逻辑坐标（DP）。
        // 与 WindowOverlay 内部的 RawX/RawY 坐标转换保持一致。
        var point = new Point(
            motion.RawX / density,
            motion.RawY / density);

        switch (motion.ActionMasked)
        {
            case MotionEventActions.Down:
                {
                    _androidIsDragging = false;

                    // 判断触摸位置是否命中 Overlay 元素。
                    _androidPointerCaptured =
                        WindowElements.Any(x => x.Contains(point));

                    if (_androidPointerCaptured)
                    {
                        BeginPointer(point);
                    }

                    Debug.WriteLine(
                        $"[Overlay.Android] DOWN: " +
                        $"X={point.X:F1}, Y={point.Y:F1}, " +
                        $"Hit={_androidPointerCaptured}");

                    break;
                }

            case MotionEventActions.Move:
                {
                    if (!_androidPointerCaptured)
                        break;

                    // 只有允许拖动的元素才执行移动。
                    if (_dragElement == null)
                        break;

                    float dx = (float)(point.X - _dragStartPoint.X);
                    float dy = (float)(point.Y - _dragStartPoint.Y);

                    // 过滤手指轻微抖动。
                    if (!_androidIsDragging &&
                        dx * dx + dy * dy <
                        DragThreshold * DragThreshold)
                    {
                        break;
                    }

                    _androidIsDragging = true;

                    if (_androidTouchView == null)
                        break;

                    MovePointer(
                        point,
                        _androidTouchView.Width / density,
                        _androidTouchView.Height / density,
                        suppressTapOnDrag: false);

                    Debug.WriteLine(
                        $"[Overlay.Android] MOVE: " +
                        $"X={point.X:F1}, Y={point.Y:F1}");

                    break;
                }

            case MotionEventActions.Up:
                {
                    if (_androidPointerCaptured)
                    {
                        // 没有发生拖动才触发点击。
                        if (!_androidIsDragging)
                        {
                            RaiseTapAt(point);

                            Debug.WriteLine(
                                "[Overlay.Android] TAP");
                        }

                        EndPointer();
                    }

                    _androidPointerCaptured = false;
                    _androidIsDragging = false;

                    break;
                }

            case MotionEventActions.Cancel:
                {
                    EndPointer();

                    _androidPointerCaptured = false;
                    _androidIsDragging = false;

                    break;
                }
        }

        // 只拦截已经命中 Overlay 元素的触摸。
        // 其他区域继续允许页面接收触摸事件。
        e.Handled = _androidPointerCaptured;
    }

    /// <summary>
    /// 取消 Android 事件订阅。
    /// </summary>
    partial void OnDetachPlatformInput()
    {
        if (_androidTouchView != null)
        {
            _androidTouchView.Touch -= OnAndroidTouch;
            _androidTouchView = null;
        }

        EndPointer();

        _androidPointerCaptured = false;
        _androidIsDragging = false;
    }    
    #endregion

#else

// 其他平台暂时使用 WindowOverlay 自带的点击事件。
partial void OnAttachPlatformInput()
{
    Tapped += OnWindowOverlayTapped;
}

partial void OnDetachPlatformInput()
{
    Tapped -= OnWindowOverlayTapped;
    EndPointer();
}

#endif
}