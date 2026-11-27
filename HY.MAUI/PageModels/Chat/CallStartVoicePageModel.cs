using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.RTC;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Controls;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Models.MsgVM;
using HY.MAUI.Services;
using HY.MAUI.Stores;
using HY.MAUI.Tools;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace HY.MAUI.PageModels.Chat
{
    public partial class CallStartVoicePageModel : ObservableObject, IQueryAttributable
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




        public CallStartVoicePageModel(IServiceProvider serviceProvider, CallService callService)
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

            // 将 WebRTC 交给全局通话服务
            _callService.StartOrAttach(_webRTC, _callId, TargetName, TargetAvatar, CallType.Voice, _startAt, _isCaller);
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

        #endregion



        private bool _isAttached;

        [RelayCommand]
        async Task Appearing()
        {
            if (_isAttached) return;

            _isAttached = true;

            StartTime = DateTime.UtcNow - _startAt;

            _timer = Application.Current!.Dispatcher.CreateTimer();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.IsRepeating = true;
            _timer.Tick += DispatcherTimer_Tick;
            _timer.Start();

            _webRTC.OnConnectionStateChanged += OnConnectionStateChanged_WebRTC;

            UpdateStatusColor(_callService.ConnectionState);

            // 只发送一次 Offer
            await _callService.EnsureOfferSentAsync();
        }

        [RelayCommand]
        void Disappearing()
        {
            if (!_isAttached) return;

            _isAttached = false;

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= DispatcherTimer_Tick;
            }

            _webRTC.OnConnectionStateChanged -= OnConnectionStateChanged_WebRTC;
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
