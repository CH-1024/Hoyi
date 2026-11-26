using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.RTC;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Controls;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Models.MsgVM;
using HY.MAUI.Pages.Chat;
using HY.MAUI.Stores;
using HY.MAUI.Tools;
using SIPSorcery.Net;

namespace HY.MAUI.Services;

public sealed class CallService
{
    private sealed class CallSession
    {
        public required WebRTCService WebRTC { get; init; }
        public required string CallId { get; init; }
        public required string TargetName { get; init; }
        public required string TargetAvatar { get; init; }
        public required CallType CallType { get; init; }
        public required DateTime StartAt { get; init; }
        public required bool IsCaller { get; init; }

        public bool OfferSent { get; set; }
        public bool ConnectedReported { get; set; }
    }

    private readonly object _gate = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly IGlobalMapper _globalMapper;

    private readonly ChatHubSignalR _chatHub;
    private readonly CallApi _callApi;
    private readonly ChatStore _chatStore;

    private CallSession? _session;
    private bool _isMinimized;
    private int _restoreInProgress;

    public event EventHandler? StateChanged;

    public bool IsActive
    {
        get { lock (_gate) return _session != null; }
    }

    public bool IsMinimized
    {
        get { lock (_gate) return _isMinimized; }
    }

    public string? CallId
    {
        get { lock (_gate) return _session?.CallId; }
    }

    public string? TargetName
    {
        get { lock (_gate) return _session?.TargetName; }
    }

    public string? TargetAvatar
    {
        get { lock (_gate) return _session?.TargetAvatar; }
    }

    public CallType? CurrentCallType
    {
        get { lock (_gate) return _session?.CallType; }
    }

    public DateTime StartAt
    {
        get { lock (_gate) return _session?.StartAt ?? DateTime.UtcNow; }
    }

    public TimeSpan Elapsed => DateTime.UtcNow - StartAt;

    public RTCPeerConnectionState ConnectionState { get; private set; } = RTCPeerConnectionState.@new;


    public CallService(IServiceProvider serviceProvider, IGlobalMapper globalMapper, ChatHubSignalR chatHub, CallApi callApi, ChatStore chatStore)
    {
        _serviceProvider = serviceProvider;
        _globalMapper = globalMapper;
        _chatHub = chatHub;
        _callApi = callApi;
        _chatStore = chatStore;
    }



    #region 接听

    private void OnConnectionStateChanged(RTCPeerConnectionState state)
    {
        ConnectionState = state;

        CallSession? session;
        bool reportConnected = false;

        lock (_gate)
        {
            session = _session;

            if (session == null)
                return;

            if (state == RTCPeerConnectionState.connected && session.IsCaller && !session.ConnectedReported)
            {
                session.ConnectedReported = true;
                reportConnected = true;
            }
        }

        if (reportConnected && session != null)
            _ = ReportConnectedAsync(session.CallId);

        NotifyChanged();
    }

    private void OnCallAbnormal(string callId)
    {
        if (CallId != callId)
            return;

        _ = EndCurrentCallAsync("通话异常断开");
    }

    private void OnCallHangUp(string callId)
    {
        if (CallId != callId)
            return;

        _ = EndCurrentCallAsync();
    }

    private void OnCallExpiry(string callId)
    {
        if (CallId != callId)
            return;

        _ = EndCurrentCallAsync("通话请求超时");
    }

    private Task<bool> OnReceiveMessage(MessageDto msgDto)
    {
        CallSession? session;

        lock (_gate)
            session = _session;

        if (session == null)
            return Task.FromResult(false);

        var chat = _chatStore.GetChat(msgDto);
        if (chat != null && chat.Type == ChatType.Private)
        {
            // 私聊 从联系人列表获取发送者信息

            var messageVM = _globalMapper.ToVM(msgDto);
            bool isCallMessage = session.CallType switch
            {
                CallType.Video => messageVM is CallVideoMessageVM video && video.CallId == session.CallId,
                CallType.Voice => messageVM is CallVoiceMessageVM voice && voice.CallId == session.CallId,
                _ => false
            };

            if (!isCallMessage) return Task.FromResult(false);

            if (!messageVM.IsSelf && chat.Unread_Count > 0)
                chat.Unread_Count--;

            return Task.FromResult(true);
        }
        else if (chat != null && chat.Type == ChatType.Group)
        {
            // 群聊 从群成员列表获取发送者信息

            throw new NotImplementedException("群聊视频通话消息处理未实现");
        }
        return Task.FromResult(false);
    }

    #endregion





    private VideoCallOverlay? _videoCallOverlay;
    private VoiceCallOverlay? _voiceCallOverlay;

    public void StartOrAttach(WebRTCService webRTC, string callId, string targetName, string targetAvatar, CallType callType, DateTime startAt, bool isCaller)
    {
        lock (_gate)
        {
            // 恢复通话时，允许重新绑定原来的通话实例
            if (_session != null)
            {
                if (_session.CallId == callId && ReferenceEquals(_session.WebRTC, webRTC))
                    return;

                throw new InvalidOperationException("当前已经存在其他通话");
            }

            var window = Application.Current!.Windows[0];
            if (callType == CallType.Voice)
            {
                _voiceCallOverlay = new VoiceCallOverlay(window, this);
                window.AddOverlay(_voiceCallOverlay);
            }
            else
            {
                _videoCallOverlay = new VideoCallOverlay(window, this);
                window.AddOverlay(_videoCallOverlay);
            }

            _session = new CallSession
            {
                WebRTC = webRTC,
                CallId = callId,
                TargetName = targetName,
                TargetAvatar = targetAvatar,
                CallType = callType,
                StartAt = startAt,
                IsCaller = isCaller,
            };

            _isMinimized = false;

            _chatHub.OnCallAbnormal_ChatHub += OnCallAbnormal;
            _chatHub.OnCallHangUp_ChatHub += OnCallHangUp;
            _chatHub.OnCallExpiry_ChatHub += OnCallExpiry;
            _chatHub.OnReceiveMessage_ChatHub += OnReceiveMessage;

            webRTC.OnConnectionStateChanged += OnConnectionStateChanged;
        }

        NotifyChanged();
    }

    public async Task EnsureOfferSentAsync()
    {
        CallSession? session;

        lock (_gate)
        {
            session = _session;

            if (session == null || !session.IsCaller || session.OfferSent)
                return;

            // 先标记，避免连续 Appearing 导致重复发送
            session.OfferSent = true;
        }

        try
        {
            await session.WebRTC.SendOffer();
        }
        catch
        {
            lock (_gate)
            {
                if (ReferenceEquals(_session, session))
                    session.OfferSent = false;
            }

            throw;
        }
    }

    public async Task HangUpAsync()
    {
        CallSession? session;

        lock (_gate)
            session = _session;

        if (session == null)
            return;

        var resp = await _callApi.HangUpCall(session.CallId);
        if (!resp.IsSucc)
        {
            await UI.RunTask(async () =>
            {
                await Shell.Current.DisplayAlertAsync("提示", $"挂断失败：{resp.Msg}", "确定");
            });

            return;
        }

        await EndCurrentCallAsync();
    }

    public void Minimize()
    {
        lock (_gate)
        {
            if (_session == null)
                return;

            _isMinimized = true;
        }

        NotifyChanged();
    }

    public async Task RestoreAsync()
    {
        // 防止重复恢复
        if (Interlocked.CompareExchange(ref _restoreInProgress, 1, 0) != 0)
            return;

        CallSession? session = null;

        try
        {
            lock (_gate)
            {
                session = _session;

                if (session == null || !_isMinimized)
                    return;

                _isMinimized = false;
            }

            NotifyChanged();

            var route = session.CallType == CallType.Video ? nameof(CallStartVideoPage) :
                        session.CallType == CallType.Voice ? nameof(CallStartVoicePage) :
                        throw new ArgumentException("不支持的通话类型");

            await UI.RunTask(async () =>
            {
                var param = CreateNavigationParameters(session);
                await Shell.Current.GoToAsync(route, false, param);
            });
        }
        catch
        {
            // 导航失败时，恢复悬浮窗
            lock (_gate)
            {
                if (session != null && ReferenceEquals(_session, session))
                {
                    _isMinimized = true;
                }
            }

            NotifyChanged();

            throw;
        }
        finally
        {
            Interlocked.Exchange(ref _restoreInProgress, 0);
        }
    }






    private static Dictionary<string, object> CreateNavigationParameters(CallSession session)
    {
        return new Dictionary<string, object>
        {
            ["WebRTC"] = session.WebRTC,
            ["IsCaller"] = session.IsCaller,
            ["CallId"] = session.CallId,
            ["TargetAvatar"] = session.TargetAvatar,
            ["TargetName"] = session.TargetName,
            ["StartAt"] = session.StartAt
        };
    }

    private async Task EndCurrentCallAsync(string? alertMessage = null)
    {
        CallSession? session;
        bool wasMinimized;

        lock (_gate)
        {
            session = _session;

            if (session == null)
                return;

            wasMinimized = _isMinimized;

            // 先清空，防止重复的 SignalR 事件重复释放
            _session = null;
            _isMinimized = false;

            _chatHub.OnCallAbnormal_ChatHub -= OnCallAbnormal;
            _chatHub.OnCallHangUp_ChatHub -= OnCallHangUp;
            _chatHub.OnCallExpiry_ChatHub -= OnCallExpiry;
            _chatHub.OnReceiveMessage_ChatHub -= OnReceiveMessage;

            session.WebRTC.OnConnectionStateChanged -= OnConnectionStateChanged;
        }

        try
        {
            session.WebRTC.Dispose();
        }
        catch (Exception ex)
        {
            //System.Diagnostics.Debug.WriteLine(ex);
        }

        NotifyChanged();

        await UI.RunTask(async () =>
        {
            var window = Application.Current!.Windows[0];
            if (CurrentCallType == CallType.Voice)
            {
                window.RemoveOverlay(_voiceCallOverlay);
            }
            else
            {
                window.RemoveOverlay(_videoCallOverlay);
            }

            // 如果通话页面还在最上层，才需要返回上一页
            if (!wasMinimized && Shell.Current != null)
                await Shell.Current.GoToAsync("..", false);

            if (!string.IsNullOrWhiteSpace(alertMessage) && Shell.Current?.CurrentPage != null)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("提示", alertMessage, "确定");
            }
        });
    }

    private async Task ReportConnectedAsync(string callId)
    {
        try
        {
            await _callApi.CallConnected(callId);
        }
        catch (Exception ex)
        {
            //System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void NotifyChanged()
    {
        UI.Run(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }


}
