
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;

namespace HY.MAUI.Controls
{
    public class CircularProgressBar : GraphicsView, IDrawable
    {
        private double _animatedProgress;
        private double _glowScale = 1.0;
        private double _glowAlpha = 0.5;

        private const string ProgressAnimationName = "ProgressAnimation";
        private const string GlowAnimationName = "GlowAnimation";

        public CircularProgressBar()
        {
            Drawable = this;
            _animatedProgress = Progress;

            Loaded += (_, _) => StartGlowAnimation();
            Unloaded += (_, _) => this.AbortAnimation(GlowAnimationName);
        }

        #region BindableProperty

        /// <summary>
        /// 满进度值，默认 1，也可以设置为 100。
        /// </summary>
        public static readonly BindableProperty MaxProgressProperty = BindableProperty.Create(nameof(MaxProgress), typeof(double), typeof(CircularProgressBar), 1d, propertyChanged: OnProgressConfigurationChanged);

        public double MaxProgress
        {
            get => (double)GetValue(MaxProgressProperty);
            set => SetValue(MaxProgressProperty, value);
        }

        /// <summary>
        /// 当前进度，范围 0 ~ MaxProgress。
        /// </summary>
        public static readonly BindableProperty ProgressProperty = BindableProperty.Create(nameof(Progress), typeof(double), typeof(CircularProgressBar), 0d, propertyChanged: OnProgressChanged);

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        /// <summary>
        /// 进度条颜色。
        /// </summary>
        public static readonly BindableProperty ProgressColorProperty = BindableProperty.Create(nameof(ProgressColor), typeof(Color), typeof(CircularProgressBar), Colors.DodgerBlue, propertyChanged: OnPropertyChanged);

        public Color ProgressColor
        {
            get => (Color)GetValue(ProgressColorProperty);
            set => SetValue(ProgressColorProperty, value);
        }

        /// <summary>
        /// 圆环线宽。
        /// </summary>
        public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(nameof(StrokeThickness), typeof(float), typeof(CircularProgressBar), 4f, propertyChanged: OnPropertyChanged);

        public float StrokeThickness
        {
            get => (float)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        /// <summary>
        /// 中心百分比文字颜色。
        /// </summary>
        public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(CircularProgressBar), Colors.Black, propertyChanged: OnPropertyChanged);

        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        /// <summary>
        /// 中心百分比文字大小。
        /// </summary>
        public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(float), typeof(CircularProgressBar), 16f, propertyChanged: OnPropertyChanged);

        public float FontSize
        {
            get => (float)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        /// <summary>
        /// 进度动画持续时间，单位毫秒。
        /// </summary>
        public static readonly BindableProperty AnimationDurationProperty = BindableProperty.Create(nameof(AnimationDuration), typeof(uint), typeof(CircularProgressBar), 500u);

        public uint AnimationDuration
        {
            get => (uint)GetValue(AnimationDurationProperty);
            set => SetValue(AnimationDurationProperty, value);
        }

        /// <summary>
        /// 是否启用末端呼吸光晕。
        /// </summary>
        public static readonly BindableProperty EnableGlowAnimationProperty = BindableProperty.Create(nameof(EnableGlowAnimation), typeof(bool), typeof(CircularProgressBar), true, propertyChanged: OnGlowAnimationChanged);

        public bool EnableGlowAnimation
        {
            get => (bool)GetValue(EnableGlowAnimationProperty);
            set => SetValue(EnableGlowAnimationProperty, value);
        }

        /// <summary>
        /// 绘制区域内边距。
        /// </summary>
        public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(CircularProgressBar), new Thickness(6), propertyChanged: OnPropertyChanged);

        public Thickness Padding
        {
            get => (Thickness)GetValue(PaddingProperty);
            set => SetValue(PaddingProperty, value);
        }

        #endregion

        #region PropertyChanged

        private static void OnPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((CircularProgressBar)bindable).Invalidate();
        }

        private static void OnProgressChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((CircularProgressBar)bindable).AnimateProgress();
        }

        private static void OnProgressConfigurationChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((CircularProgressBar)bindable).AnimateProgress();
        }

        private static void OnGlowAnimationChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (CircularProgressBar)bindable;

            if (control.EnableGlowAnimation)
            {
                control.StartGlowAnimation();
            }
            else
            {
                control.AbortAnimation(GlowAnimationName);
                control._glowScale = 1.0;
                control._glowAlpha = 0.5;
                control.Invalidate();
            }
        }

        #endregion

        #region ProgressAnimation

        /// <summary>
        /// 平滑过渡到目标进度。
        /// </summary>
        private void AnimateProgress()
        {
            double max = MaxProgress;

            if (!double.IsFinite(max) || max <= 0 || !double.IsFinite(Progress))
            {
                this.AbortAnimation(ProgressAnimationName);
                Invalidate();
                return;
            }

            double target = Math.Clamp(Progress, 0d, max);

            // 停止上一次动画，从当前显示值继续。
            this.AbortAnimation(ProgressAnimationName);

            if (AnimationDuration == 0)
            {
                _animatedProgress = target;
                Invalidate();
                return;
            }

            double start = _animatedProgress;

            var animation = new Animation(
                callback: value =>
                {
                    _animatedProgress = start + (target - start) * value;

                    Invalidate();
                },
                start: 0,
                end: 1,
                easing: Easing.CubicOut);

            animation.Commit(
                owner: this,
                name: ProgressAnimationName,
                rate: 16,
                length: AnimationDuration,
                easing: Easing.Linear);
        }

        #endregion

        #region GlowAnimation

        /// <summary>
        /// 启动循环呼吸光晕动画。
        /// </summary>
        private void StartGlowAnimation()
        {
            this.AbortAnimation(GlowAnimationName);

            if (!EnableGlowAnimation || !IsVisible) return;

            var animation = new Animation();

            animation.Add(0, 1, new Animation(value =>
            {
                _glowScale = 1.0 + value * 0.6;
                _glowAlpha = 0.8 - value * 0.6;

                Invalidate();
            }, 0, 1, Easing.Linear));

            animation.Commit(
                owner: this,
                name: GlowAnimationName,
                rate: 16,
                length: 1000,
                easing: Easing.Linear,
                repeat: () => EnableGlowAnimation && IsVisible);
        }

        #endregion

        #region Draw

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            double max = MaxProgress;

            if (!double.IsFinite(max) || max <= 0 || !double.IsFinite(_animatedProgress) || !float.IsFinite(StrokeThickness) || StrokeThickness <= 0)
            {
                return;
            }

            // 根据 Padding 缩小有效绘制区域。
            Thickness padding = Padding;

            float left = dirtyRect.Left + (float)padding.Left;
            float top = dirtyRect.Top + (float)padding.Top;
            float right = dirtyRect.Right - (float)padding.Right;
            float bottom = dirtyRect.Bottom - (float)padding.Bottom;

            float width = right - left;
            float height = bottom - top;

            float size = Math.Min(width, height);

            if (size <= StrokeThickness || width <= 0 || height <= 0) return;

            float centerX = left + width / 2f;
            float centerY = top + height / 2f;
            float radius = (size - StrokeThickness) / 2f;

            float ratio = (float)Math.Clamp(_animatedProgress / max, 0d, 1d);

            // 1. 绘制顺时针进度圆环。
            if (ratio > 0)
            {
                canvas.StrokeColor = ProgressColor;
                canvas.StrokeSize = StrokeThickness;
                canvas.StrokeLineCap = LineCap.Round;

                if (ratio >= 1f)
                {
                    canvas.DrawCircle(centerX, centerY, radius);
                }
                else
                {
                    float sweepAngle = ratio * 360f;

                    int segments = Math.Max(2, (int)Math.Ceiling(sweepAngle / 2f));

                    var path = new PathF();

                    for (int i = 0; i <= segments; i++)
                    {
                        float angle = sweepAngle * i / segments;
                        float radians = angle * (MathF.PI / 180f);

                        float x = centerX + radius * MathF.Sin(radians);
                        float y = centerY - radius * MathF.Cos(radians);

                        if (i == 0) path.MoveTo(x, y);
                        else path.LineTo(x, y);
                    }

                    canvas.DrawPath(path);
                }

                // 2. 绘制圆环末端呼吸光晕。
                if (EnableGlowAnimation)
                {
                    float angle = ratio * 2f * MathF.PI;

                    float glowX = centerX + radius * MathF.Sin(angle);
                    float glowY = centerY - radius * MathF.Cos(angle);

                    float glowRadius = StrokeThickness * (float)_glowScale;

                    canvas.FillColor = ProgressColor.WithAlpha((float)Math.Clamp(_glowAlpha, 0d, 1d));

                    canvas.FillCircle(glowX, glowY, glowRadius);
                }
            }

            // 3. 绘制中心百分比数字。
            int percentage = (int)Math.Round(ratio * 100f, MidpointRounding.AwayFromZero);

            canvas.FontColor = TextColor;
            canvas.FontSize = FontSize;
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;

            canvas.DrawString($"{percentage}%", centerX - size / 2f, centerY - size / 2f, size, size, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        #endregion
    }
}
