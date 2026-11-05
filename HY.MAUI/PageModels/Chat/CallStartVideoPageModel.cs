using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.RTC;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Mapping;
using HY.MAUI.Models.MsgVM;
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
        readonly IGlobalCache _globalCache;
        readonly ChatHubSignalR _chatHub;
        readonly ChatStore _chatStore;
        readonly ContactStore _contactStore;
        readonly CallApi _callApi;

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


        public CallStartVideoPageModel(IGlobalCache globalCache, ChatHubSignalR chatHub, ChatStore chatStore, ContactStore contactStore, CallApi callApi)
        {
            _globalCache = globalCache;
            _chatHub = chatHub;
            _chatStore = chatStore;
            _contactStore = contactStore;
            _callApi = callApi;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            _webRTC = (WebRTCService)query["WebRTC"];
            _isCaller = (bool)query["IsCaller"];
            _callId = query["CallId"]?.ToString();
            _startAt = (DateTime)query["StartAt"];
            TargetAvatar = query["TargetAvatar"]?.ToString();
            TargetName = query["TargetName"]?.ToString();
        }


        #region 接听

        private async void OnCallAbnormal_ChatHub(string callId)
        {
            if (callId == _callId)
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", "通话异常断开", "退出");
                await Shell.Current.GoToAsync("..", false);
            }
        }

        private async void OnCallHangUp_ChatHub(string callId)
        {
            if (callId == _callId)
            {
                //_ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", "通话已在其他设备处理", "退出");
                await Shell.Current.GoToAsync("..", false);
            }
        }

        private async void OnCallExpiry_ChatHub(string callId)
        {
            if (callId == _callId)
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", "请求超时", "退出");
                await Shell.Current.GoToAsync("..", false);
            }
        }

        private Task<bool> OnReceiveMessage_ChatHub(MessageDto msgDto)
        {
            var currentUser = _globalCache.GetCurrentUser();
            var chat = _chatStore.GetChat(currentUser.Id, msgDto);
            if (chat != null && chat.Type == ChatType.Private)
            {
                // 私聊 从联系人列表获取发送者信息

                var contact = _contactStore.GetContact(msgDto.Sender_Id);
                var messageVM = msgDto.ToVM(currentUser.Id, contact?.Avatar, contact?.Nickname);
                if (messageVM is CallVideoMessageVM videoCallMsg && videoCallMsg.CallId == _callId)
                {
                    if (!messageVM.IsSelf)
                    {
                        if (chat.Unread_Count > 0)
                        {
                            chat.Unread_Count -= 1;
                        }
                    }

                    return Task.FromResult(true);
                }
            }
            else if (chat != null && chat.Type == ChatType.Group)
            {
                // 群聊 从群成员列表获取发送者信息

                throw new NotImplementedException("群聊视频通话消息处理未实现");
            }
            return Task.FromResult(false);
        }

        private void DispatcherTimer_Tick(object? sender, EventArgs e)
        {
            StartTime = DateTime.UtcNow - _startAt;
        }

        #endregion



        #region 视频

        bool _hasConnected;
        private void OnConnectionStateChanged_WebRTC(RTCPeerConnectionState state)
        {
            if (state == RTCPeerConnectionState.connected)
            {
                if (_isCaller && !_hasConnected)
                {
                    _hasConnected = true;
                    _ = _callApi.CallConnected(_callId);
                }
                StatusColor = Colors.LightGreen;
            }
            else if (state == RTCPeerConnectionState.connecting)
            {
                StatusColor = Colors.Orange;
            }
            else if (state == RTCPeerConnectionState.disconnected)
            {
                StatusColor = Colors.Red;
            }
            else if (state == RTCPeerConnectionState.closed)
            {
                StatusColor = Colors.Gray;
            }
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


        [RelayCommand]
        async Task Appearing()
        {
            _bigCanvas.PaintSurface += Local_PaintSurface;
            _smallCanvas.PaintSurface += Remote_PaintSurface;

            _bigCanvas.InvalidateSurface();
            _smallCanvas.InvalidateSurface();


            _timer = Application.Current!.Dispatcher.CreateTimer();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.IsRepeating = true;
            _timer.Tick += DispatcherTimer_Tick;
            _timer.Start();

            _chatHub.OnCallAbnormal_ChatHub += OnCallAbnormal_ChatHub;
            _chatHub.OnCallHangUp_ChatHub += OnCallHangUp_ChatHub;
            _chatHub.OnCallExpiry_ChatHub += OnCallExpiry_ChatHub;
            _chatHub.OnReceiveMessage_ChatHub += OnReceiveMessage_ChatHub;

            _webRTC.OnConnectionStateChanged += OnConnectionStateChanged_WebRTC;
            _webRTC.OnLocalVideoFrameReceived += OnLocalVideoFrameReceived_WebRTC;
            _webRTC.OnRemoteVideoFrameReceived += OnRemoteVideoFrameReceived_WebRTC;

            if (_isCaller)
            {
                await _webRTC.SendOffer();
            }
        }


        [RelayCommand]
        void Disappearing()
        {
            _bigCanvas.PaintSurface -= Local_PaintSurface;
            _smallCanvas.PaintSurface -= Remote_PaintSurface;


            _timer.Stop();
            _timer.Tick -= DispatcherTimer_Tick;

            _chatHub.OnCallAbnormal_ChatHub -= OnCallAbnormal_ChatHub;
            _chatHub.OnCallHangUp_ChatHub -= OnCallHangUp_ChatHub;
            _chatHub.OnCallExpiry_ChatHub -= OnCallExpiry_ChatHub;
            _chatHub.OnReceiveMessage_ChatHub -= OnReceiveMessage_ChatHub;

            _webRTC.OnConnectionStateChanged -= OnConnectionStateChanged_WebRTC;
            _webRTC.OnLocalVideoFrameReceived -= OnLocalVideoFrameReceived_WebRTC;
            _webRTC.OnRemoteVideoFrameReceived -= OnRemoteVideoFrameReceived_WebRTC;

            _webRTC.Dispose();
            _webRTC = null;
        }


        [RelayCommand]
        async Task HangUp()
        {
            var resp = await _callApi.HangUpCall(_callId);
            if (resp.IsSucc) await Shell.Current.GoToAsync("..", false);
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

    }
}
