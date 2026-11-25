using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Interfaces;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Vpx.Net;

namespace HY.MAUI.Communication.RTC
{
    public class WebRTCService : IDisposable
    {
        string _callId;
        bool _isTest;

        ChatHubSignalR _chatHub;

        private IVideoRecorderService _videoService;
        private IAudioRecorderService _audioService;

        private RTCConfiguration _configuration;
        private RTCPeerConnection _peerConnection;

        public event Action<RTCPeerConnectionState> OnConnectionStateChanged;
        public event Action<uint, int, int, byte[], VideoPixelFormatsEnum> OnLocalVideoFrameReceived;
        public event Action<byte[], uint, uint, VideoPixelFormatsEnum> OnRemoteVideoFrameReceived;


        public WebRTCService()
        {
            _chatHub = MauiProgram.Services.GetService<ChatHubSignalR>()!;
            _videoService = MauiProgram.Services.GetService<IVideoRecorderService>()!;
            _audioService = MauiProgram.Services.GetService<IAudioRecorderService>()!;

            var useTurnServer = false;
            var gatherTime = 2000;

            _configuration = new RTCConfiguration
            {
                bundlePolicy = RTCBundlePolicy.balanced,
                iceTransportPolicy = RTCIceTransportPolicy.all,
                X_GatherTimeoutMs = gatherTime,
                iceServers =
                [
                    //new RTCIceServer { urls = "stun:stun.l.google.com:19302" },

                    new() { urls = "turn:hoyi.net.cn:3478", username = "cheng", credential = "12345678" },
                    new() { urls = "stun:hoyi.net.cn:3478", username = "cheng", credential = "12345678" },

                    //new() { urls = "stun:stun.xten.com:3478" },
                    //new() { urls = "stun:stun.voipbuster.com:3478" },
                    //new() { urls = "stun:stun.sipgate.net:3478" },
                    //new() { urls = "stun:stun.l.google.com:19302" },
                    //new() { urls = "stun:stun1.l.google.com:19302" },
                    //new() { urls = "stun:stun2.l.google.com:19302" },
                    //new() { urls = "stun:stun3.l.google.com:19302" },
                    //new() { urls = "stun:stun4.l.google.com:19302" },

                    #region MyRegion

                    //new() { urls = "stun:51.83.201.84:3478" },
                    //new() { urls = "stun:69.20.59.115:3478" },
                    //new() { urls = "stun:188.138.90.169:3478" },
                    //new() { urls = "stun:35.158.233.7:3478" },
                    //new() { urls = "stun:5.39.72.109:3478" },
                    //new() { urls = "stun:185.125.180.70:3478" },
                    //new() { urls = "stun:89.37.98.122:3478" },
                    //new() { urls = "stun:108.163.134.186:3478" },
                    //new() { urls = "stun:212.53.40.40:3478" },
                    //new() { urls = "stun:5.161.52.174:3478" },
                    //new() { urls = "stun:45.15.102.34:3478" },
                    //new() { urls = "stun:24.204.48.11:3478" },
                    //new() { urls = "stun:52.24.174.49:3478" },
                    //new() { urls = "stun:80.156.214.187:3478" },
                    //new() { urls = "stun:192.76.120.66:3478" },
                    //new() { urls = "stun:195.201.132.113:3478" },
                    //new() { urls = "stun:90.145.158.66:3478" },
                    //new() { urls = "stun:129.153.212.128:3478" },
                    //new() { urls = "stun:54.197.117.0:3478" },
                    //new() { urls = "stun:3.121.78.82:3478" },
                    //new() { urls = "stun:195.208.107.138:3478" },
                    //new() { urls = "stun:81.82.206.117:3478" },
                    //new() { urls = "stun:51.255.31.35:3478" },
                    //new() { urls = "stun:157.161.10.32:3478" },
                    //new() { urls = "stun:20.93.239.173:3478" },
                    //new() { urls = "stun:217.91.243.229:3478" },
                    //new() { urls = "stun:35.180.81.93:3478" },
                    //new() { urls = "stun:52.47.70.236:3478" },
                    //new() { urls = "stun:5.161.57.75:3478" },
                    //new() { urls = "stun:212.18.0.14:3478" },
                    //new() { urls = "stun:34.192.137.246:3478" },
                    //new() { urls = "stun:209.251.63.76:3478" },
                    //new() { urls = "stun:213.203.211.131:3478" },
                    //new() { urls = "stun:31.184.236.23:3478" },
                    //new() { urls = "stun:62.72.83.10:3478" },
                    //new() { urls = "stun:198.100.144.121:3478" },
                    //new() { urls = "stun:194.149.74.158:3478" },
                    //new() { urls = "stun:95.216.78.222:3478" },
                    //new() { urls = "stun:88.218.220.40:3478" },
                    //new() { urls = "stun:193.182.111.151:3478" },
                    //new() { urls = "stun:23.21.199.62:3478" },
                    //new() { urls = "stun:212.53.40.43:3478" },
                    //new() { urls = "stun:44.230.252.214:3478" },
                    //new() { urls = "stun:192.172.233.145:3478" },
                    //new() { urls = "stun:81.83.12.46:3478" },
                    //new() { urls = "stun:51.68.112.203:3478" },
                    //new() { urls = "stun:79.140.42.88:3478" },
                    //new() { urls = "stun:3.78.237.53:3478" },
                    //new() { urls = "stun:34.74.124.204:3478" },
                    //new() { urls = "stun:52.52.70.85:3478" },
                    //new() { urls = "stun:95.216.145.84:3478" },
                    //new() { urls = "stun:212.103.68.7:3478" },
                    //new() { urls = "stun:51.83.15.212:3478" },
                    //new() { urls = "stun:34.206.168.53:3478" },
                    //new() { urls = "stun:188.40.203.74:3478" },
                    //new() { urls = "stun:52.26.251.34:3478" },
                    //new() { urls = "stun:51.68.45.75:3478" },
                    //new() { urls = "stun:212.144.246.197:3478" },
                    //new() { urls = "stun:91.213.98.54:3478" },
                    //new() { urls = "stun:23.21.92.55:3478" },
                    //new() { urls = "stun:143.198.60.79:3478" },
                    //new() { urls = "stun:159.69.191.124:443" },
                    //new() { urls = "stun:185.88.236.76:3478" },
                    //new() { urls = "stun:66.228.54.23:3478" },
                    //new() { urls = "stun:136.243.59.79:3478" },
                    //new() { urls = "stun:195.145.93.141:3478" },
                    //new() { urls = "stun:34.195.177.19:3478" },
                    //new() { urls = "stun:159.69.191.124:3478" },
                    //new() { urls = "stun:49.12.125.53:3478" },
                    //new() { urls = "stun:213.251.48.147:3478" },
                    //new() { urls = "stun:91.212.41.85:3478" },
                    //new() { urls = "stun:172.233.245.118:3478" },
                    //new() { urls = "stun:188.40.18.246:3478" },
                    //new() { urls = "stun:35.177.202.92:3478" },
                    //new() { urls = "stun:88.99.67.241:3478" },
                    //new() { urls = "stun:80.155.54.123:3478" },
                    //new() { urls = "stun:147.182.188.245:3478" },
                    //new() { urls = "stun:193.22.17.97:3478" },
                    //new() { urls = "stun:176.9.24.184:3478" },
                    //new() { urls = "stun:137.74.112.113:3478" },
                    //new() { urls = "stun:85.197.87.182:3478" },
                    //new() { urls = "stun:81.3.27.44:3478" },

                    new() { urls = "stun:stun.12voip.com:3478" },
                    new() { urls = "stun:stun.aa.net.uk:3478" },
                    new() { urls = "stun:stun.acrobits.cz:3478" },
                    new() { urls = "stun:stun.actionvoip.com:3478" },
                    new() { urls = "stun:stun.annatel.net:3478" },
                    new() { urls = "stun:stun.antisip.com:3478" },
                    new() { urls = "stun:stun.cablenet-as.net:3478" },
                    new() { urls = "stun:stun.cheapvoip.com:3478" },
                    new() { urls = "stun:stun.commpeak.com:3478" },
                    new() { urls = "stun:stun.cope.es:3478" },
                    new() { urls = "stun:stun.dcalling.de:3478" },
                    new() { urls = "stun:stun.dus.net:3478" },
                    new() { urls = "stun:stun.easyvoip.com:3478" },
                    new() { urls = "stun:stun.epygi.com:3478" },
                    new() { urls = "stun:stun.freecall.com:3478" },
                    new() { urls = "stun:stun.freeswitch.org:3478" },
                    new() { urls = "stun:stun.freevoipdeal.com:3478" },
                    new() { urls = "stun:stun.halonet.pl:3478" },
                    new() { urls = "stun:stun.hoiio.com:3478" },
                    new() { urls = "stun:stun.infra.net:3478" },
                    new() { urls = "stun:stun.internetcalls.com:3478" },
                    new() { urls = "stun:stun.intervoip.com:3478" },
                    new() { urls = "stun:stun.ipfire.org:3478" },
                    new() { urls = "stun:stun.ippi.fr:3478" },
                    new() { urls = "stun:stun.it1.hr:3478" },
                    new() { urls = "stun:stun.jumblo.com:3478" },
                    new() { urls = "stun:stun.justvoip.com:3478" },
                    new() { urls = "stun:stun.l.google.com:19302" },
                    new() { urls = "stun:stun.l.google.com:19302" },
                    new() { urls = "stun:stun.linphone.org:3478" },
                    new() { urls = "stun:stun.liveo.fr:3478" },
                    new() { urls = "stun:stun.lowratevoip.com:3478" },
                    new() { urls = "stun:stun.miwifi.com:3478" },
                    new() { urls = "stun:stun.myvoiptraffic.com:3478" },
                    new() { urls = "stun:stun.mywatson.it:3478" },
                    new() { urls = "stun:stun.netappel.com:3478" },
                    new() { urls = "stun:stun.netgsm.com.tr:3478" },
                    new() { urls = "stun:stun.nfon.net:3478" },
                    new() { urls = "stun:stun.nonoh.net:3478" },
                    new() { urls = "stun:stun.ooma.com:3478" },
                    new() { urls = "stun:stun.pjsip.org:3478" },
                    new() { urls = "stun:stun.poivy.com:3478" },
                    new() { urls = "stun:stun.powervoip.com:3478" },
                    new() { urls = "stun:stun.ppdi.com:3478" },
                    new() { urls = "stun:stun.rockenstein.de:3478" },
                    new() { urls = "stun:stun.rolmail.net:3478" },
                    new() { urls = "stun:stun.rynga.com:3478" },
                    new() { urls = "stun:stun.sip.us:3478" },
                    new() { urls = "stun:stun.sipdiscount.com:3478" },
                    new() { urls = "stun:stun.siplogin.de:3478" },
                    new() { urls = "stun:stun.sipnet.net:3478" },
                    new() { urls = "stun:stun.sipnet.ru:3478" },
                    new() { urls = "stun:stun.siptraffic.com:3478" },
                    new() { urls = "stun:stun.smartvoip.com:3478" },
                    new() { urls = "stun:stun.smsdiscount.com:3478" },
                    new() { urls = "stun:stun.solcon.nl:3478" },
                    new() { urls = "stun:stun.solnet.ch:3478" },
                    new() { urls = "stun:stun.sonetel.com:3478" },
                    new() { urls = "stun:stun.sonetel.net:3478" },
                    new() { urls = "stun:stun.srce.hr:3478" },
                    new() { urls = "stun:stun.tel.lu:3478" },
                    new() { urls = "stun:stun.telbo.com:3478" },
                    new() { urls = "stun:stun.t-online.de:3478" },
                    new() { urls = "stun:stun.twt.it:3478" },
                    new() { urls = "stun:stun.uls.co.za:3478" },
                    new() { urls = "stun:stun.usfamily.net:3478" },
                    new() { urls = "stun:stun.vo.lu:3478" },
                    new() { urls = "stun:stun.voicetrading.com:3478" },
                    new() { urls = "stun:stun.voip.aebc.com:3478" },
                    new() { urls = "stun:stun.voip.blackberry.com:3478" },
                    new() { urls = "stun:stun.voip.eutelia.it:3478" },
                    new() { urls = "stun:stun.voipblast.com:3478" },
                    new() { urls = "stun:stun.voipbuster.com:3478" },
                    new() { urls = "stun:stun.voipbusterpro.com:3478" },
                    new() { urls = "stun:stun.voipcheap.com:3478" },
                    new() { urls = "stun:stun.voipfibre.com:3478" },
                    new() { urls = "stun:stun.voipgain.com:3478" },
                    new() { urls = "stun:stun.voipinfocenter.com:3478" },
                    new() { urls = "stun:stun.voipplanet.nl:3478" },
                    new() { urls = "stun:stun.voippro.com:3478" },
                    new() { urls = "stun:stun.voipraider.com:3478" },
                    new() { urls = "stun:stun.voipstunt.com:3478" },
                    new() { urls = "stun:stun.voipwise.com:3478" },
                    new() { urls = "stun:stun.voipzoom.com:3478" },
                    new() { urls = "stun:stun.voys.nl:3478" },
                    new() { urls = "stun:stun.voztele.com:3478" },
                    new() { urls = "stun:stun.webcalldirect.com:3478" },
                    new() { urls = "stun:stun.zadarma.com:3478" },
                    new() { urls = "stun:stun1.l.google.com:19302" },
                    new() { urls = "stun:stun1.l.google.com:19302" },
                    new() { urls = "stun:stun2.l.google.com:19302" },
                    new() { urls = "stun:stun2.l.google.com:19302" },
                    new() { urls = "stun:stun3.l.google.com:19302" },
                    new() { urls = "stun:stun3.l.google.com:19302" },
                    new() { urls = "stun:stun4.l.google.com:19302" },
                    new() { urls = "stun:stun4.l.google.com:19302" },
                    #endregion

                ]
            };
        }

        private void HandleIce(string signal)
        {
            if (_peerConnection == null) return;

            var ice = JsonSerializer.Deserialize<RTCIceCandidateInit>(signal);

            var init = new RTCIceCandidateInit
            {
                candidate = ice.candidate,
                sdpMid = ice.sdpMid,
                sdpMLineIndex = ice.sdpMLineIndex
            };
            _peerConnection.addIceCandidate(init);
        }

        private async void HandleOffer(string signal)
        {
            if (_peerConnection == null) return;

            var offer = JsonSerializer.Deserialize<RTCSessionDescriptionInit>(signal);

            var init = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offer.sdp
            };
            _peerConnection.setRemoteDescription(init);

            var answer = _peerConnection.createAnswer();
            await _peerConnection.setLocalDescription(answer);

            await _chatHub.SendAnswer(_callId, JsonSerializer.Serialize(answer));
        }

        private async void HandleAnswer(string signal)
        {
            if (_peerConnection == null) return;

            var answer = JsonSerializer.Deserialize<RTCSessionDescriptionInit>(signal);

            var init = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = answer.sdp
            };
            _peerConnection.setRemoteDescription(init);

            await Task.CompletedTask;
        }

        private async void OnSendIce(RTCIceCandidate candidate)
        {
            if (candidate.type == RTCIceCandidateType.host || candidate.type == RTCIceCandidateType.prflx) return;

            var ice = new RTCIceCandidateInit
            {
                candidate = candidate.candidate,
                sdpMid = candidate.sdpMid,
                sdpMLineIndex = candidate.sdpMLineIndex
            };

            await _chatHub.SendIce(_callId, JsonSerializer.Serialize(ice));
        }

        private void Video_OnConnectionStateChange(RTCPeerConnectionState state)
        {
            OnConnectionStateChanged?.Invoke(state);

            switch (state)
            {
                case RTCPeerConnectionState.connected:
                    if (_isTest) _videoService?.StartTesting();
                    else _videoService?.StartRecording();
                    _audioService?.StartRecording();
                    break;
                case RTCPeerConnectionState.failed:
                    _peerConnection?.Close("ice disconnection");
                    break;
                case RTCPeerConnectionState.closed:
                    _videoService?.StopRecording();
                    _audioService?.StopRecording();
                    break;
            }
        }

        private void Audio_OnConnectionStateChange(RTCPeerConnectionState state)
        {
            OnConnectionStateChanged?.Invoke(state);

            switch (state)
            {
                case RTCPeerConnectionState.connected:
                    _audioService?.StartRecording();
                    break;
                case RTCPeerConnectionState.failed:
                    _peerConnection?.Close("ice disconnection");
                    break;
                case RTCPeerConnectionState.closed:
                    _audioService?.StopRecording();
                    break;
            }
        }



        public Task Video_Initialize(string callId, bool isTest = false)
        {
            _callId = callId;
            _isTest = isTest;

            _chatHub.OnReceiveIce_ChatHub += HandleIce;
            _chatHub.OnReceiveOffer_ChatHub += HandleOffer;
            _chatHub.OnReceiveAnswer_ChatHub += HandleAnswer;

            _peerConnection = new RTCPeerConnection(_configuration);

            _peerConnection.onicecandidate += OnSendIce;
            _peerConnection.onconnectionstatechange += Video_OnConnectionStateChange;

            #region VP8
            _audioService.SetAudioEncoder(new AudioEncoder());
            _videoService.SetVideoEncoder(new VP8Codec());

            // 本地
            _audioService.OnAudioSourceEncodedSample += _peerConnection.SendAudio;
            _videoService.OnVideoSourceEncodedSample += _peerConnection.SendVideo;
            _videoService.OnVideoSourceRawSample += (dur, width, height, sample, format) => OnLocalVideoFrameReceived?.Invoke(dur, width, height, sample, format);

            // 远程
            _peerConnection.OnAudioFrameReceived += _audioService.GotAudioFrame;
            _peerConnection.OnVideoFrameReceived += _videoService.GotVideoFrame;
            _videoService.OnVideoSinkDecodedSample += (sample, width, height, format) => OnRemoteVideoFrameReceived?.Invoke(sample, width, height, format);
            #endregion

            var audioTrack = new MediaStreamTrack(_audioService.GetSelectedFormat(), MediaStreamStatusEnum.SendRecv);
            _peerConnection.addTrack(audioTrack);

            var videoSourceTrack = new MediaStreamTrack(_videoService.GetSelectedFormat(), MediaStreamStatusEnum.SendOnly);
            _peerConnection.addTrack(videoSourceTrack);

            var videoSinkTrack = new MediaStreamTrack(_videoService.GetSelectedFormat(), MediaStreamStatusEnum.RecvOnly);
            _peerConnection.addTrack(videoSinkTrack);

            return Task.CompletedTask;
        }

        public Task Audio_Initialize(string callId)
        {
            _callId = callId;

            _chatHub.OnReceiveIce_ChatHub += HandleIce;
            _chatHub.OnReceiveOffer_ChatHub += HandleOffer;
            _chatHub.OnReceiveAnswer_ChatHub += HandleAnswer;

            _peerConnection = new RTCPeerConnection(_configuration);

            _peerConnection.onicecandidate += OnSendIce;
            _peerConnection.onconnectionstatechange += Audio_OnConnectionStateChange;

            _audioService.SetAudioEncoder(new AudioEncoder());

            // 本地
            _audioService.OnAudioSourceEncodedSample += _peerConnection.SendAudio;

            // 远程
            _peerConnection.OnAudioFrameReceived += _audioService.GotAudioFrame;

            var audioTrack = new MediaStreamTrack(_audioService.GetSelectedFormat(), MediaStreamStatusEnum.SendRecv);
            _peerConnection.addTrack(audioTrack);

            return Task.CompletedTask;
        }

        public async Task SendOffer()
        {
            var offer = _peerConnection.createOffer();
            await _peerConnection.setLocalDescription(offer);

            await _chatHub.SendOffer(_callId, JsonSerializer.Serialize(offer));
        }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _chatHub.OnReceiveIce_ChatHub -= HandleIce;
            _chatHub.OnReceiveOffer_ChatHub -= HandleOffer;
            _chatHub.OnReceiveAnswer_ChatHub -= HandleAnswer;

            _audioService?.StopRecording();
            _videoService?.StopRecording();

            try
            {
                _peerConnection?.close();
            }
            finally
            {
                _peerConnection?.Dispose();
            }
            
            _chatHub = null;
            _audioService = null;
            _videoService = null;
            _configuration = null;
            _peerConnection = null;
        }

    }
}
