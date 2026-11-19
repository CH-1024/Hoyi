using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace HY.MAUI.Controls
{
    public class AnimatedEllipsisLabel : ContentView
    {
        private readonly Label _label;

        private CancellationTokenSource? _cts;
        private int _dotCount;

        #region BindableProperties

        public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(AnimatedEllipsisLabel), string.Empty, propertyChanged: OnDisplayPropertyChanged);

        public static readonly BindableProperty IsAnimationEnabledProperty = BindableProperty.Create(nameof(IsAnimationEnabled), typeof(bool), typeof(AnimatedEllipsisLabel), true, propertyChanged: OnIsAnimationEnabledChanged);

        public static readonly BindableProperty IntervalProperty = BindableProperty.Create(nameof(Interval), typeof(int), typeof(AnimatedEllipsisLabel), 500);

        public static readonly BindableProperty MaxDotsProperty = BindableProperty.Create(nameof(MaxDots), typeof(int), typeof(AnimatedEllipsisLabel), 3);

        public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(AnimatedEllipsisLabel), 14d, propertyChanged: OnDisplayPropertyChanged);

        public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(AnimatedEllipsisLabel), null, propertyChanged: OnDisplayPropertyChanged);

        public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(AnimatedEllipsisLabel), FontAttributes.None, propertyChanged: OnDisplayPropertyChanged);

        public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(AnimatedEllipsisLabel), null, propertyChanged: OnDisplayPropertyChanged);

        public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(nameof(HorizontalTextAlignment), typeof(TextAlignment), typeof(AnimatedEllipsisLabel), TextAlignment.Start, propertyChanged: OnDisplayPropertyChanged);

        public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(nameof(VerticalTextAlignment), typeof(TextAlignment), typeof(AnimatedEllipsisLabel), TextAlignment.Center, propertyChanged: OnDisplayPropertyChanged);

        public static readonly BindableProperty LineBreakModeProperty = BindableProperty.Create(nameof(LineBreakMode), typeof(LineBreakMode), typeof(AnimatedEllipsisLabel), LineBreakMode.WordWrap, propertyChanged: OnDisplayPropertyChanged);

        public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(nameof(MaxLines), typeof(int), typeof(AnimatedEllipsisLabel), -1, propertyChanged: OnDisplayPropertyChanged);

        #endregion

        #region Properties

        public string? Text
        {
            get => (string?)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsAnimationEnabled
        {
            get => (bool)GetValue(IsAnimationEnabledProperty);
            set => SetValue(IsAnimationEnabledProperty, value);
        }

        public int Interval
        {
            get => (int)GetValue(IntervalProperty);
            set => SetValue(IntervalProperty, value);
        }

        public int MaxDots
        {
            get => (int)GetValue(MaxDotsProperty);
            set => SetValue(MaxDotsProperty, value);
        }

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public Color? TextColor
        {
            get => (Color?)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public FontAttributes FontAttributes
        {
            get => (FontAttributes)GetValue(FontAttributesProperty);
            set => SetValue(FontAttributesProperty, value);
        }

        public string? FontFamily
        {
            get => (string?)GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public TextAlignment HorizontalTextAlignment
        {
            get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty);
            set => SetValue(HorizontalTextAlignmentProperty, value);
        }

        public TextAlignment VerticalTextAlignment
        {
            get => (TextAlignment)GetValue(VerticalTextAlignmentProperty);
            set => SetValue(VerticalTextAlignmentProperty, value);
        }

        public LineBreakMode LineBreakMode
        {
            get => (LineBreakMode)GetValue(LineBreakModeProperty);
            set => SetValue(LineBreakModeProperty, value);
        }

        public int MaxLines
        {
            get => (int)GetValue(MaxLinesProperty);
            set => SetValue(MaxLinesProperty, value);
        }

        #endregion

        public AnimatedEllipsisLabel()
        {
            _label = new Label
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center
            };

            Content = _label;

            UpdateDisplay();
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            if (Handler != null)
            {
                UpdateDisplay();
                StartAnimation();
            }
            else
            {
                StopAnimation();
            }
        }

        protected override void OnPropertyChanged(string? propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (propertyName == nameof(Padding))
            {
                _label.Padding = Padding;
            }
        }

        #region PropertyChanged

        private static void OnDisplayPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (AnimatedEllipsisLabel)bindable;
            control.UpdateDisplay();
        }

        private static void OnIsAnimationEnabledChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (AnimatedEllipsisLabel)bindable;

            if ((bool)newValue)
            {
                control._dotCount = 0;
                control.UpdateDisplay();
                control.StartAnimation();
            }
            else
            {
                control.StopAnimation();
            }
        }

        #endregion

        #region Display

        private void UpdateDisplay()
        {
            if (_label == null)
                return;

            // 原始文本 + 动态点号
            _label.Text = (Text ?? string.Empty) + (IsAnimationEnabled ? new string('.', _dotCount) : string.Empty);

            // 同步 Label 外观
            _label.FontSize = FontSize;
            _label.TextColor = TextColor;
            _label.FontAttributes = FontAttributes;
            _label.FontFamily = FontFamily;

            _label.HorizontalTextAlignment = HorizontalTextAlignment;
            _label.VerticalTextAlignment = VerticalTextAlignment;

            _label.LineBreakMode = LineBreakMode;
            _label.MaxLines = MaxLines;
            _label.Padding = Padding;
        }

        #endregion

        #region Animation

        private void StartAnimation()
        {
            if (!IsAnimationEnabled || Handler == null || _cts != null)
                return;

            var cts = new CancellationTokenSource();
            _cts = cts;

            _ = AnimateAsync(cts);
        }

        private void StopAnimation()
        {
            var cts = _cts;

            _cts = null;

            if (cts != null)
                cts.Cancel();

            _dotCount = 0;

            UpdateDisplay();
        }

        private async Task AnimateAsync(CancellationTokenSource cts)
        {
            var token = cts.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(Math.Max(Interval, 1), token);

                    if (token.IsCancellationRequested)
                        break;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (token.IsCancellationRequested || !ReferenceEquals(_cts, cts) || Handler == null || !IsAnimationEnabled)
                        {
                            return;
                        }

                        int maxDots = Math.Max(MaxDots, 0);

                        if (_dotCount >= maxDots) _dotCount = 0;
                        else _dotCount++;

                        UpdateDisplay();
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            finally
            {
                if (ReferenceEquals(_cts, cts))
                    _cts = null;

                cts.Dispose();
            }
        }

        #endregion
    }
}