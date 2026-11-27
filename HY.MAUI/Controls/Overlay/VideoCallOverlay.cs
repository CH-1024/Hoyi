using HY.MAUI.Communication.RTC;
using HY.MAUI.Services;
using Microsoft.Maui.Graphics.Platform;
using SIPSorceryMedia.Abstractions;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using IImage = Microsoft.Maui.Graphics.IImage;

namespace HY.MAUI.Controls
{
    public sealed class VideoCallOverlay : BaseOverlay
    {
        private readonly CallService _callService;
        private readonly VideoCallElement _element;
        private readonly WebRTCService _webRTC;

        private bool IsOverlayVisible => _callService.IsActive && _callService.IsMinimized;

        private int _invalidatePending;
        private bool _deinitialized;

        private readonly object _frameLock = new();

        private IImage? _remoteFrame;
        private IImage? _localFrame;

        public VideoCallOverlay(Window window, CallService callService, WebRTCService webRTC) : base(window)
        {
            _callService = callService;
            _element = new VideoCallElement(this, callService);
            _webRTC = webRTC;

            AddOverlayElement(_element, OverlayInteraction.TapAndDrag);

            ElementTapped += OnElementTapped;
            _callService.StateChanged += OnCallStateChanged;
            _webRTC.OnLocalVideoFrameReceived += OnLocalVideoFrameReceived;
            _webRTC.OnRemoteVideoFrameReceived += OnRemoteVideoFrameReceived;

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
            _deinitialized = true;
            ElementTapped -= OnElementTapped;
            _callService.StateChanged -= OnCallStateChanged;
            _webRTC.OnLocalVideoFrameReceived -= OnLocalVideoFrameReceived;
            _webRTC.OnRemoteVideoFrameReceived -= OnRemoteVideoFrameReceived;

            IImage? local;
            IImage? remote;

            lock (_frameLock)
            {
                local = _localFrame;
                remote = _remoteFrame;

                _localFrame = null;
                _remoteFrame = null;
            }

            local?.Dispose();
            remote?.Dispose();

            return base.Deinitialize();
        }

        private void OnCallStateChanged(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(UpdateState);
        }

        private void OnLocalVideoFrameReceived(uint duration, int width, int height, byte[] sample, VideoPixelFormatsEnum format)
        {
            if (!_callService.IsMinimized)
                return;

            try
            {
                // 本地摄像头目前输出 I420。
                byte[] bgr = format switch
                {
                    VideoPixelFormatsEnum.I420 => PixelConverter.I420toBGR(sample, width, height, out _),
                    VideoPixelFormatsEnum.Bgr => sample,
                    _ => throw new NotSupportedException($"不支持的本地视频格式：{format}")
                };

                UpdateFrame(isLocal: true, bgr, width, height);
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"本地视频帧处理失败：{ex}");
            }
        }

        private void OnRemoteVideoFrameReceived(byte[] sample, uint width, uint height, VideoPixelFormatsEnum format)
        {
            if (!_callService.IsMinimized)
                return;

            try
            {
                // 远端解码器目前输出 BGR。
                byte[] bgr = format switch
                {
                    VideoPixelFormatsEnum.Bgr => sample,
                    VideoPixelFormatsEnum.I420 => PixelConverter.I420toBGR(sample, (int)width, (int)height, out _),
                    _ => throw new NotSupportedException($"不支持的远端视频格式：{format}")
                };

                UpdateFrame(isLocal: false, bgr, (int)width, (int)height);
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"远端视频帧处理失败：{ex}");
            }
        }

        private void UpdateState()
        {
            //bool visible = _callService.IsActive && _callService.IsMinimized;

            //if (visible && !_timerRunning)
            //{
            //    _timer.Start();
            //    _timerRunning = true;
            //}
            //else if (!visible && _timerRunning)
            //{
            //    _timer.Stop();
            //    _timerRunning = false;
            //}

            Invalidate();
        }


        #region MyRegion

        private static IImage? CreateImageFromBgr(byte[] bgr, int width, int height)
        {
            if (width <= 0 || height <= 0)
                return null;

            int requiredLength = checked(width * height * 3);

            if (bgr.Length < requiredLength)
                return null;

            // 创建 BGRA Bitmap。
            using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);

            IntPtr pixels = bitmap.GetPixels();

            if (pixels == IntPtr.Zero)
                return null;

            // BGR24 转 BGRA8888。
            unsafe
            {
                fixed (byte* src = bgr)
                {
                    byte* dst = (byte*)pixels;

                    int srcStride = width * 3;
                    int dstStride = bitmap.RowBytes;

                    for (int y = 0; y < height; y++)
                    {
                        byte* srcRow = src + y * srcStride;
                        byte* dstRow = dst + y * dstStride;

                        for (int x = 0; x < width; x++)
                        {
                            dstRow[x * 4] = srcRow[x * 3];
                            dstRow[x * 4 + 1] = srcRow[x * 3 + 1];
                            dstRow[x * 4 + 2] = srcRow[x * 3 + 2];
                            dstRow[x * 4 + 3] = 255;
                        }
                    }
                }
            }

            // 编码为 JPEG。
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, 85);

            if (encoded == null)
                return null;

            // 转成 MAUI Graphics IImage。
            byte[] imageBytes = encoded.ToArray();

            using var stream = new MemoryStream(imageBytes, writable: false);

            return PlatformImage.FromStream(stream, ImageFormat.Jpeg);
        }

        private void UpdateFrame(bool isLocal, byte[] bgr, int width, int height)
        {
            // 创建新图片。
            IImage? newFrame = CreateImageFromBgr(bgr, width, height);

            if (newFrame == null)
                return;

            IImage? oldFrame;

            lock (_frameLock)
            {
                // Overlay 已释放，不能再接收新图片。
                if (_deinitialized)
                {
                    newFrame.Dispose();
                    return;
                }

                if (isLocal)
                {
                    oldFrame = _localFrame;
                    _localFrame = newFrame;
                }
                else
                {
                    oldFrame = _remoteFrame;
                    _remoteFrame = newFrame;
                }
            }

            // 替换成功后释放旧图片。
            oldFrame?.Dispose();

            // 通知 Overlay 重绘。
            RequestInvalidate();
        }

        private void RequestInvalidate()
        {
            if (_deinitialized)
                return;

            // 合并重复重绘请求。
            if (Interlocked.Exchange(ref _invalidatePending, 1) != 0)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Interlocked.Exchange(ref _invalidatePending, 0);

                if (!_deinitialized && IsOverlayVisible)
                {
                    Invalidate();
                }
            });
        }

        private void DrawVideoFrame(ICanvas canvas, RectF bounds, bool isLocal)
        {
            canvas.SaveState();

            // 限制绘制区域，防止视频超出小窗。
            canvas.ClipRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);

            canvas.FillColor = Colors.Transparent;
            canvas.FillRectangle(bounds);

            lock (_frameLock)
            {
                IImage? image = isLocal ? _localFrame : _remoteFrame;

                if (image != null && image.Width > 0 && image.Height > 0)
                {
                    // AspectFill，铺满目标区域。
                    float scale = Math.Max(bounds.Width / image.Width, bounds.Height / image.Height);

                    float drawWidth = image.Width * scale;
                    float drawHeight = image.Height * scale;

                    float x = bounds.X + (bounds.Width - drawWidth) / 2;

                    float y = bounds.Y + (bounds.Height - drawHeight) / 2;

                    canvas.DrawImage(image, x, y, drawWidth, drawHeight);
                }
            }

            canvas.RestoreState();
        }

        #endregion


        private sealed class VideoCallElement : IOverlayElement, IMovableOverlayElement
        {
            private const float DefaultWidth = 120f;
            private const float DefaultHeight = 200f;

            // 本地视频固定尺寸
            private const float LocalWidth = 30f;
            private const float LocalHeight = 50f;

            private readonly VideoCallOverlay _owner;
            private readonly CallService _callService;

            public RectF Bounds { get; private set; }

            public float X { get; set; }
            public float Y { get; set; } = DeviceInfo.Platform == DevicePlatform.WinUI ? 30f : 35f;

            public float Width => DefaultWidth;
            public float Height => DefaultHeight;

            public VideoCallElement(VideoCallOverlay owner, CallService callService)
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

                // 整个浮动窗
                Bounds = new RectF(X, Y, Width, Height);

                canvas.SaveState();

                // 第一层：远端视频铺满
                _owner.DrawVideoFrame(canvas, Bounds, isLocal: false);

                // 第二层：本地视频覆盖右上角
                var localBounds = new RectF(Bounds.Right - LocalWidth, Bounds.Top, LocalWidth, LocalHeight);

                _owner.DrawVideoFrame(canvas, localBounds, isLocal: true);

                canvas.RestoreState();
            }

        }
    }
}
