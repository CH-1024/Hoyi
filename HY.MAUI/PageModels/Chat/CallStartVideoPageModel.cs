using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.RTC;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Models.MsgVM;
using HY.MAUI.Services;
using HY.MAUI.Stores;
using HY.MAUI.Tools;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;

namespace HY.MAUI.PageModels.Chat
{
    public partial class CallStartVideoPageModel : ObservableObject, IQueryAttributable
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly CallService _callService;

        IDispatcherTimer _timer;

        private string targetAvatar;
        public string TargetAvatar
        {
            get { return targetAvatar; }
            set { SetProperty(ref targetAvatar, value); }
        }

        private string targetName;
        public string TargetName
        {
            get { return targetName; }
            set { SetProperty(ref targetName, value); }
        }

        private TimeSpan startTime;
        public TimeSpan StartTime
        {
            get { return startTime; }
            set { SetProperty(ref startTime, value); }
        }

        private Brush statusColor = Colors.Gray;
        public Brush StatusColor
        {
            get { return statusColor; }
            set { SetProperty(ref statusColor, value); }
        }

        bool _isCaller;
        WebRTCService _webRTC;
        string _callId;
        DateTime _startAt;


        public SKCanvasView _bigCanvas;
        public SKCanvasView _smallCanvas;

        SKBitmap? _localFrame;
        readonly object _localLock = new object();

        SKBitmap? _remoteFrame;
        readonly object _remoteLock = new object();


        public CallStartVideoPageModel(IServiceProvider serviceProvider, CallService callService)
        {
            _serviceProvider = serviceProvider;
            _callService = callService;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            _webRTC = (WebRTCService)query["WebRTC"];
            _isCaller = (bool)query["IsCaller"];
            _callId = query["CallId"]?.ToString() ?? string.Empty;

            _startAt = (DateTime)query["StartAt"];

            TargetAvatar = query["TargetAvatar"]?.ToString() ?? string.Empty;
            TargetName = query["TargetName"]?.ToString() ?? string.Empty;

            _callService.StartOrAttach(_webRTC, _callId, TargetName, TargetAvatar, CallType.Video, _startAt, _isCaller);
        }

        private void DispatcherTimer_Tick(object? sender, EventArgs e)
        {
            StartTime = DateTime.UtcNow - _startAt;
        }



        #region RTC

        private void OnConnectionStateChanged_WebRTC(RTCPeerConnectionState state)
        {
            UpdateStatusColor(state);
        }

        private void OnLocalVideoFrameReceived_WebRTC(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
        {
            var bgr = PixelConverter.I420toBGR(sample, width, height, out _);

            int w = width;
            int h = height;

            // 创建或重用 SKBitmap
            if (_localFrame == null || _localFrame.Width != w || _localFrame.Height != h)
            {
                _localFrame?.Dispose();
                _localFrame = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);
            }

            lock (_localLock)
            {
                unsafe
                {
                    byte* src = (byte*)System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(bgr, 0);

                    byte* dst = (byte*)_localFrame.GetPixels();

                    int srcStride = w * 3;
                    int dstStride = _localFrame.RowBytes;

                    for (int y = 0; y < h; y++)
                    {
                        byte* srcRow = src + y * srcStride;
                        byte* dstRow = dst + y * dstStride;

                        for (int x = 0; x < w; x++)
                        {
                            dstRow[x * 4 + 0] = srcRow[x * 3 + 0]; // B
                            dstRow[x * 4 + 1] = srcRow[x * 3 + 1]; // G
                            dstRow[x * 4 + 2] = srcRow[x * 3 + 2]; // R
                            dstRow[x * 4 + 3] = 255;               // A
                        }
                    }
                }

                UI.Run(() => _bigCanvas?.InvalidateSurface());
            }
        }

        private void OnRemoteVideoFrameReceived_WebRTC(byte[] sample, uint width, uint height, VideoPixelFormatsEnum pixelFormat)
        {
            var bgr = sample;

            int w = (int)width;
            int h = (int)height;

            // 创建或重用 SKBitmap
            if (_remoteFrame == null || _remoteFrame.Width != w || _remoteFrame.Height != h)
            {
                _remoteFrame?.Dispose();
                _remoteFrame = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);
            }

            lock (_remoteLock)
            {
                unsafe
                {
                    byte* src = (byte*)System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(bgr, 0);

                    byte* dst = (byte*)_remoteFrame.GetPixels();

                    int srcStride = w * 3;
                    int dstStride = _remoteFrame.RowBytes;

                    for (int y = 0; y < h; y++)
                    {
                        byte* srcRow = src + y * srcStride;
                        byte* dstRow = dst + y * dstStride;

                        for (int x = 0; x < w; x++)
                        {
                            dstRow[x * 4 + 0] = srcRow[x * 3 + 0]; // B
                            dstRow[x * 4 + 1] = srcRow[x * 3 + 1]; // G
                            dstRow[x * 4 + 2] = srcRow[x * 3 + 2]; // R
                            dstRow[x * 4 + 3] = 255;               // A
                        }
                    }
                }

                UI.Run(() => _smallCanvas?.InvalidateSurface());
            }
        }

        #endregion



        private void Local_PaintSurface(object? sender, SKPaintSurfaceEventArgs args)
        {
            var surface = args.Surface;
            var canvas = surface.Canvas;

            // 清除画布
            canvas.Clear(SKColors.Transparent);

            // 检查是否有有效帧
            if (_localFrame == null) return;

            // 锁定帧数据避免更新冲突
            lock (_localLock)
            {
                if (_localFrame == null) return;

                // 计算缩放比例以保持宽高比
                float scale = Math.Min((float)args.Info.Width / _localFrame.Width, (float)args.Info.Height / _localFrame.Height);

                // 计算目标矩形（居中显示）
                float scaledWidth = _localFrame.Width * scale;
                float scaledHeight = _localFrame.Height * scale;
                float x = (args.Info.Width - scaledWidth) / 2;
                float y = (args.Info.Height - scaledHeight) / 2;
                var destRect = new SKRect(x, y, x + scaledWidth, y + scaledHeight);

                // 绘制帧
                canvas.DrawBitmap(_localFrame, destRect, SKSamplingOptions.Default);
            }
        }
        private void Remote_PaintSurface(object? sender, SKPaintSurfaceEventArgs args)
        {
            var surface = args.Surface;
            var canvas = surface.Canvas;

            // 清除画布
            canvas.Clear(SKColors.Transparent);

            // 检查是否有有效帧
            if (_remoteFrame == null) return;

            // 锁定帧数据避免更新冲突
            lock (_remoteLock)
            {
                if (_remoteFrame == null) return;

                // 计算缩放比例以保持宽高比
                float scale = Math.Min((float)args.Info.Width / _remoteFrame.Width, (float)args.Info.Height / _remoteFrame.Height);

                // 计算目标矩形（居中显示）
                float scaledWidth = _remoteFrame.Width * scale;
                float scaledHeight = _remoteFrame.Height * scale;
                float x = (args.Info.Width - scaledWidth) / 2;
                float y = (args.Info.Height - scaledHeight) / 2;
                var destRect = new SKRect(x, y, x + scaledWidth, y + scaledHeight);

                // 绘制帧
                canvas.DrawBitmap(_remoteFrame, destRect, SKSamplingOptions.Default);
            }
        }

        private bool _isAttached;

        [RelayCommand]
        async Task Appearing()
        {
            if (_isAttached) return;

            _isAttached = true;

            _bigCanvas.PaintSurface += Local_PaintSurface;
            _smallCanvas.PaintSurface += Remote_PaintSurface;

            _bigCanvas.InvalidateSurface();
            _smallCanvas.InvalidateSurface();

            StartTime = DateTime.UtcNow - _startAt;

            _timer = Application.Current!.Dispatcher.CreateTimer();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.IsRepeating = true;
            _timer.Tick += DispatcherTimer_Tick;
            _timer.Start();

            _webRTC.OnConnectionStateChanged += OnConnectionStateChanged_WebRTC;
            _webRTC.OnLocalVideoFrameReceived += OnLocalVideoFrameReceived_WebRTC;
            _webRTC.OnRemoteVideoFrameReceived += OnRemoteVideoFrameReceived_WebRTC;

            UpdateStatusColor(_callService.ConnectionState);

            await _callService.EnsureOfferSentAsync();
        }

        [RelayCommand]
        void Disappearing()
        {
            if (!_isAttached) return;

            _isAttached = false;

            _bigCanvas.PaintSurface -= Local_PaintSurface;
            _smallCanvas.PaintSurface -= Remote_PaintSurface;

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= DispatcherTimer_Tick;
            }

            _webRTC.OnConnectionStateChanged -= OnConnectionStateChanged_WebRTC;
            _webRTC.OnLocalVideoFrameReceived -= OnLocalVideoFrameReceived_WebRTC;
            _webRTC.OnRemoteVideoFrameReceived -= OnRemoteVideoFrameReceived_WebRTC;

            // 页面退出后释放 UI 画面缓存
            lock (_localLock)
            {
                _localFrame?.Dispose();
                _localFrame = null;
            }

            lock (_remoteLock)
            {
                _remoteFrame?.Dispose();
                _remoteFrame = null;
            }
        }


        [RelayCommand]
        async Task Minimize()
        {
            _callService.Minimize();

            await Shell.Current.GoToAsync("..", false);
        }

        [RelayCommand]
        Task HangUp()
        {
            return _callService.HangUpAsync();
        }

        bool isLocalInBig = true;
        [RelayCommand]
        async Task Tap()
        {
            _bigCanvas.PaintSurface -= Local_PaintSurface;
            _smallCanvas.PaintSurface -= Remote_PaintSurface;
            _bigCanvas.PaintSurface -= Remote_PaintSurface;
            _smallCanvas.PaintSurface -= Local_PaintSurface;

            if (isLocalInBig)
            {
                _bigCanvas.PaintSurface += Remote_PaintSurface;
                _smallCanvas.PaintSurface += Local_PaintSurface;
            }
            else
            {
                _bigCanvas.PaintSurface += Local_PaintSurface;
                _smallCanvas.PaintSurface += Remote_PaintSurface;
            }

            isLocalInBig = !isLocalInBig;
        }





        private void UpdateStatusColor(RTCPeerConnectionState state)
        {
            StatusColor = state switch
            {
                RTCPeerConnectionState.connected => Colors.LightGreen,
                RTCPeerConnectionState.connecting => Colors.Orange,
                RTCPeerConnectionState.disconnected => Colors.Red,
                RTCPeerConnectionState.failed => Colors.Red,
                _ => Colors.Gray
            };
        }

    }
}
