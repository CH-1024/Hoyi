using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace HY.MAUI.Controls
{
    public class CircularProgressBar : GraphicsView, IDrawable
    {
        public CircularProgressBar()
        {
            Drawable = this;
        }

        #region BindableProperty

        /// <summary>
        /// 满进度值，默认 1，也可以设置为 100。
        /// </summary>
        public static readonly BindableProperty MaxProgressProperty = BindableProperty.Create(nameof(MaxProgress), typeof(double), typeof(CircularProgressBar), 1d, propertyChanged: OnPropertyChanged);

        public double MaxProgress
        {
            get => (double)GetValue(MaxProgressProperty);
            set => SetValue(MaxProgressProperty, value);
        }

        /// <summary>
        /// 当前进度，范围 0 ~ MaxProgress。
        /// </summary>
        public static readonly BindableProperty ProgressProperty = BindableProperty.Create(nameof(Progress), typeof(double), typeof(CircularProgressBar), 0d, propertyChanged: OnPropertyChanged);

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public static readonly BindableProperty ProgressColorProperty = BindableProperty.Create(nameof(ProgressColor), typeof(Color), typeof(CircularProgressBar), Colors.DodgerBlue, propertyChanged: OnPropertyChanged);

        public Color ProgressColor
        {
            get => (Color)GetValue(ProgressColorProperty);
            set => SetValue(ProgressColorProperty, value);
        }

        public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(nameof(StrokeThickness), typeof(float), typeof(CircularProgressBar), 4f, propertyChanged: OnPropertyChanged);

        public float StrokeThickness
        {
            get => (float)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        #endregion

        private static void OnPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((CircularProgressBar)bindable).Invalidate();
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            double max = MaxProgress;
            double progress = Progress;
            float thickness = StrokeThickness;

            if (!double.IsFinite(max) || !double.IsFinite(progress) || max <= 0 || thickness <= 0)
            {
                return;
            }

            // 计算进度比例，保证范围为 0 ~ 1
            float ratio = (float)Math.Clamp(progress / max, 0d, 1d);

            if (ratio <= 0) return;

            float size = Math.Min(dirtyRect.Width, dirtyRect.Height);

            if (size <= thickness) return;

            float centerX = dirtyRect.Center.X;
            float centerY = dirtyRect.Center.Y;
            float radius = (size - thickness) / 2f;

            canvas.StrokeColor = ProgressColor;
            canvas.StrokeSize = thickness;
            canvas.StrokeLineCap = LineCap.Round;

            // 满进度直接绘制完整圆环
            if (ratio >= 1f)
            {
                canvas.DrawCircle(centerX, centerY, radius);
                return;
            }

            // 从顶部开始，按顺时针方向逐点绘制
            float sweepAngle = ratio * 360f;

            // 每隔约 2 度生成一个点
            int segments = Math.Max(2, (int)Math.Ceiling(sweepAngle / 2f));

            var path = new PathF();

            for (int i = 0; i <= segments; i++)
            {
                float angle = sweepAngle * i / segments;
                float radians = angle * (MathF.PI / 180f);

                // 0° 在顶部，90° 在右侧，180° 在底部
                float x = centerX + radius * MathF.Sin(radians);
                float y = centerY - radius * MathF.Cos(radians);

                if (i == 0) path.MoveTo(x, y);
                else path.LineTo(x, y);
            }

            canvas.DrawPath(path);
        }
    }
}