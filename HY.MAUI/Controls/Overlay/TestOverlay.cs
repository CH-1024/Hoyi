using HY.MAUI.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Controls
{
    public sealed class TestOverlay : BaseOverlay
    {
        private readonly BackgroundElement _background;

        public TestOverlay(Window window) : base(window)
        {
            _background = new BackgroundElement();

            // 视频悬浮窗支持拖动和点击。
            AddOverlayElement(_background, OverlayInteraction.TapAndDrag);

            ElementTapped += OnElementTapped;
        }

        private void OnElementTapped(object? sender, OverlayTappedEventArgs e)
        {
            if (ReferenceEquals(e.Element, _background))
            {
                // 展开完整通话界面。
            }
            //else if (ReferenceEquals(e.Element, _muteButton))
            //{
            //    // 切换麦克风。
            //}
            //else if (ReferenceEquals(e.Element, _hangupButton))
            //{
            //    // 挂断电话。
            //}
        }

        public override bool Deinitialize()
        {
            ElementTapped -= OnElementTapped;

            return base.Deinitialize();
        }

        private sealed class BackgroundElement : IOverlayElement, IMovableOverlayElement
        {
            private const float DefaultWidth = 200f;
            private const float DefaultHeight = 50f;

            public float X { get; set; }

            public float Y { get; set; } = 50f;

            public float Width => DefaultWidth;

            public float Height => DefaultHeight;

            public RectF Bounds { get; private set; }

            public BackgroundElement()
            {
            }

            public bool Contains(Point point)
            {
                return Bounds.Contains(point);
            }

            public void MoveTo(float x, float y)
            {
                X = x;
                Y = y;
            }

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                Bounds = new RectF(X, Y, Width, Height);

                canvas.SaveState();

                canvas.FillColor = Color.FromArgb("#252936");
                canvas.FillRoundedRectangle(Bounds, 5);

                canvas.RestoreState();
            }

        }
    }
}
