using Microsoft.Maui;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HY.MAUI.Controls;

[Flags]
public enum OverlayInteraction
{
    None = 0,
    Tap = 1,
    Drag = 2,
    TapAndDrag = Tap | Drag
}

/// <summary>
/// 所有可交互的 Overlay 绘制元素。
/// </summary>
public interface IOverlayElement : IWindowOverlayElement
{
    RectF Bounds { get; }
}

/// <summary>
/// 支持移动的 Overlay 元素。
/// </summary>
public interface IMovableOverlayElement : IOverlayElement
{
    void MoveTo(float x, float y);
}

/// <summary>
/// Overlay 统一点击事件参数。
/// </summary>
public sealed class OverlayTappedEventArgs(IOverlayElement element) : EventArgs
{
    public IOverlayElement Element { get; } = element;
}

/// <summary>
/// 所有悬浮窗的公共基类。
/// </summary>
public abstract partial class BaseOverlay : WindowOverlay
{
    private readonly List<ElementRegistration> _elements = [];

    private protected IMovableOverlayElement? _dragElement;

    private protected Point _dragStartPoint;
    private protected RectF _dragStartBounds;

    private bool _dragStarted;
    private bool _suppressNextTap;
    private bool _inputAttached;

    private protected const float DragThreshold = 4f;

    /// <summary>
    /// 统一点击事件。
    /// </summary>
    public event EventHandler<OverlayTappedEventArgs>? ElementTapped;

    protected BaseOverlay(Window window) : base(window)
    {
        EnableDrawableTouchHandling = true;
        DisableUITouchEventPassthrough = false;
    }

    #region 元素管理

    /// <summary>
    /// 注册 Overlay 绘制元素。
    /// </summary>
    protected void AddOverlayElement(IOverlayElement element, OverlayInteraction interaction = OverlayInteraction.Tap)
    {
        ArgumentNullException.ThrowIfNull(element);

        if ((interaction & OverlayInteraction.Drag) != 0 && element is not IMovableOverlayElement)
        {
            throw new ArgumentException("支持拖动的元素必须实现 IMovableOverlayElement。", nameof(element));
        }

        if (_elements.Any(x => ReferenceEquals(x.Element, element)))
            return;

        if (!AddWindowElement(element))
            return;

        _elements.Add(new ElementRegistration(element, interaction));
    }

    protected void RemoveOverlayElement(IOverlayElement element)
    {
        _elements.RemoveAll(x => ReferenceEquals(x.Element, element));

        if (ReferenceEquals(_dragElement, element))
            EndPointer();

        RemoveWindowElement(element);
    }

    #endregion

    #region 生命周期

    public override bool Initialize()
    {
        if (!base.Initialize())
            return false;

        if (!_inputAttached)
        {
            AttachPlatformInput();
            _inputAttached = true;
        }

        return true;
    }

    public override bool Deinitialize()
    {
        if (_inputAttached)
        {
            DetachPlatformInput();
            _inputAttached = false;
        }

        return base.Deinitialize();
    }

    #endregion

    #region 平台输入接口

    // 具体实现统一放在 BaseOverlay.Input.cs。
    private void AttachPlatformInput()
    {
        OnAttachPlatformInput();
    }

    private void DetachPlatformInput()
    {
        OnDetachPlatformInput();
    }

    partial void OnAttachPlatformInput();
    partial void OnDetachPlatformInput();

    #endregion

    #region 点击事件

    /// <summary>
    /// 由平台输入层调用，统一处理点击。
    /// </summary>
    private void RaiseElementTapped(IOverlayElement? element)
    {
        if (element == null)
            return;

        if (!HasInteraction(element, OverlayInteraction.Tap))
            return;

        ElementTapped?.Invoke(this, new OverlayTappedEventArgs(element));
    }

    /// <summary>
    /// Windows、Android 等使用 WindowOverlay.Tapped 的平台。
    /// </summary>
    private void OnWindowOverlayTapped(object? sender, WindowOverlayTappedEventArgs e)
    {
        if (_suppressNextTap)
        {
            _suppressNextTap = false;
            return;
        }

        // 优先处理后注册的元素，方便按钮覆盖在背景元素之上。
        foreach (var item in _elements.AsEnumerable().Reverse())
        {
            if ((item.Interaction & OverlayInteraction.Tap) == 0)
                continue;

            if (!e.WindowOverlayElements.Contains(item.Element))
                continue;

            RaiseElementTapped(item.Element);
            return;
        }
    }

    /// <summary>
    /// Mac、iOS 原生点击手势使用。
    /// </summary>
    private void RaiseTapAt(Point point)
    {
        var element = FindElementAt(point, OverlayInteraction.Tap);

        RaiseElementTapped(element);
    }

    #endregion

    #region 拖动管理

    /// <summary>
    /// 开始一次拖动识别。
    /// </summary>
    private bool BeginPointer(Point point)
    {
        _suppressNextTap = false;

        _dragElement = FindElementAt(point, OverlayInteraction.Drag) as IMovableOverlayElement;

        _dragStarted = false;

        if (_dragElement == null)
            return false;

        _dragStartPoint = point;
        _dragStartBounds = _dragElement.Bounds;

        return true;
    }

    /// <summary>
    /// 更新拖动位置。
    /// </summary>
    private void MovePointer(Point point, float surfaceWidth, float surfaceHeight, bool suppressTapOnDrag)
    {
        if (_dragElement == null)
            return;

        float dx = (float)(point.X - _dragStartPoint.X);
        float dy = (float)(point.Y - _dragStartPoint.Y);

        // 过滤轻微移动。
        if (!_dragStarted && dx * dx + dy * dy < DragThreshold * DragThreshold)
            return;

        _dragStarted = true;

        // Windows 依靠这个状态屏蔽 WindowOverlay.Tapped。
        // Mac/iOS 由手势识别器本身解决点击和拖动冲突。
        if (suppressTapOnDrag)
            _suppressNextTap = true;

        float x = _dragStartBounds.X + dx;
        float y = _dragStartBounds.Y + dy;

        float maxX = Math.Max(0, surfaceWidth - _dragStartBounds.Width);

        float maxY = Math.Max(0, surfaceHeight - _dragStartBounds.Height);

        x = Math.Clamp(x, 0, maxX);
        y = Math.Clamp(y, 0, maxY);

        _dragElement.MoveTo(x, y);

        Invalidate();
    }

    /// <summary>
    /// 结束拖动。
    /// </summary>
    private void EndPointer()
    {
        _dragElement = null;
        _dragStarted = false;
    }

    #endregion

    #region 命中检测

    private IOverlayElement? FindElementAt(Point point, OverlayInteraction interaction)
    {
        foreach (var item in _elements.AsEnumerable().Reverse())
        {
            if ((item.Interaction & interaction) == 0)
                continue;

            if (item.Element.Contains(point))
                return item.Element;
        }

        return null;
    }

    private bool HasInteraction(IOverlayElement element, OverlayInteraction interaction)
    {
        return _elements.Any(x => ReferenceEquals(x.Element, element) && (x.Interaction & interaction) != 0);
    }

    #endregion

    private sealed record ElementRegistration(IOverlayElement Element, OverlayInteraction Interaction);
}