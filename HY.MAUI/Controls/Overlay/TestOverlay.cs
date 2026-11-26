using HY.MAUI.Services;
using System;
using System.Collections.Generic;
using System.Text;

#if WINDOWS
using Microsoft.UI.Xaml.Input;
#endif

namespace HY.MAUI.Controls
{
    public sealed class TestOverlay : WindowOverlay
    {
        private readonly TestElement _element;

        public TestOverlay(Window window) : base(window)
        {
            _element = new TestElement(this);

            EnableDrawableTouchHandling = true;
            DisableUITouchEventPassthrough = false;

            AddWindowElement(_element);

            Tapped += OnTapped;
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

        private void OnTapped(object? sender, WindowOverlayTappedEventArgs e)
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
        }

        public override bool Deinitialize()
        {
            Tapped -= OnTapped;

#if WINDOWS
            DetachPointerHandlers();
#endif

            return base.Deinitialize();
        }

        private sealed class TestElement : IWindowOverlayElement
        {
            private readonly TestOverlay _owner;

            private RectF _bounds;

            private const float DefaultWidth = 200f;
            private const float DefaultHeight = 50f;

            public float X { get; set; }

            public float Y { get; set; } = 50f;

            public float Width => DefaultWidth;

            public float Height => DefaultHeight;

            public TestElement(TestOverlay owner)
            {
                _owner = owner;
            }

            public bool Contains(Point point)
            {
                return _bounds.Contains(point);
            }

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                _bounds = new RectF(X, Y, Width, Height);

                canvas.SaveState();

                canvas.FillColor = Color.FromArgb("#252936");

                canvas.FillRoundedRectangle(_bounds, 5);

                canvas.RestoreState();
            }
        }
    }
}
