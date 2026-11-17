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
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace HY.MAUI.PageModels.Chat
{
    public partial class CallStartVoicePageModel : ObservableObject, IQueryAttributable
    {
        readonly IGlobalCache _globalCache;
        readonly IGlobalMapper _globalMapper;
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




        public CallStartVoicePageModel(IGlobalCache globalCache, IGlobalMapper globalMapper, ChatHubSignalR chatHub, ChatStore chatStore, ContactStore contactStore, CallApi callApi)
        {
            _globalCache = globalCache;
            _globalMapper = globalMapper;
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
            var chat = _chatStore.GetChat(msgDto);
            if (chat != null && chat.Type == ChatType.Private)
            {
                // 私聊 从联系人列表获取发送者信息

                var messageVM = _globalMapper.ToVM(msgDto);
                if (messageVM is CallVoiceMessageVM voiceCallMsg && voiceCallMsg.CallId == _callId)
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

                throw new NotImplementedException("群聊语音通话消息处理未实现");
            }
            return Task.FromResult(false);
        }

        private void DispatcherTimer_Tick(object? sender, EventArgs e)
        {
            StartTime = DateTime.UtcNow - _startAt;
        }

        bool _hasConnected;
        private async void OnConnectionStateChanged_WebRTC(RTCPeerConnectionState state)
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




        [RelayCommand]
        async Task Appearing()
        {
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

            if (_isCaller)
            {
                await _webRTC.SendOffer();
            }
        }


        [RelayCommand]
        void Disappearing()
        {
            _timer.Stop();
            _timer.Tick -= DispatcherTimer_Tick;

            _chatHub.OnCallAbnormal_ChatHub -= OnCallAbnormal_ChatHub;
            _chatHub.OnCallHangUp_ChatHub -= OnCallHangUp_ChatHub;
            _chatHub.OnCallExpiry_ChatHub += OnCallExpiry_ChatHub;
            _chatHub.OnReceiveMessage_ChatHub -= OnReceiveMessage_ChatHub;

            _webRTC.OnConnectionStateChanged -= OnConnectionStateChanged_WebRTC;

            _webRTC.Dispose();
            _webRTC = null;
        }

        [RelayCommand]
        async Task HangUp()
        {
            var resp = await _callApi.HangUpCall(_callId);
            if (resp.IsSucc) await Shell.Current.GoToAsync("..", false);
        }

    }
}
