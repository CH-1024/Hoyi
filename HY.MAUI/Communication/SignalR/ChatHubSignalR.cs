using HY.MAUI.Communication.Auth;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.Http.Requests;
using HY.MAUI.Communication.SignalR.Requests;
using HY.MAUI.Configurations;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Mapping;
using HY.MAUI.Models;
using HY.MAUI.Models.MsgVM;
using HY.MAUI.Pages.Login;
using HY.MAUI.Stores;
using HY.MAUI.Tools;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace HY.MAUI.Communication.SignalR
{
    public class ChatHubSignalR : IAsyncDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IGlobalCache _globalCache;
        private readonly ILoginService _loginService;
        private readonly ITokenProvider _tokenProvider;
        private readonly INotificationManagerService _notificationManager;

        private readonly ChatStore _chatStore;
        private readonly ContactStore _contactStore;
        private readonly ContactRequestStore _contactRequestStore;
        private readonly MessageStore _messageStore;

        private HubConnection? _connection = null;
        private readonly SemaphoreSlim _initLock = new(1, 1);


        public ChatHubSignalR(IServiceProvider serviceProvider, IGlobalCache globalCache, ILoginService loginService, ITokenProvider tokenProvider,
                              INotificationManagerService notificationManager,
                              ChatStore chatStore, ContactStore contactStore, ContactRequestStore contactRequestStore, MessageStore messageStore)
        {
            _serviceProvider = serviceProvider;
            _globalCache = globalCache;
            _loginService = loginService;
            _tokenProvider = tokenProvider;
            _notificationManager = notificationManager;

            _chatStore = chatStore;
            _contactStore = contactStore;
            _contactRequestStore = contactRequestStore;
            _messageStore = messageStore;
        }


        public async Task<bool> InitializeAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                if (_connection != null && _connection.State != HubConnectionState.Disconnected) return false;

                _connection = BuildConnection();

                RegisterLifecycleEvents();

                RegisterHubMethods();

                await StartAsync();

                return true;
            }
            catch
            {
                await DisposeAsync().AsTask();

                return false;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task StartAsync()
        {
            if (_connection != null && _connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync();
            }
        }

        public async Task StopAsync()
        {
            if (_connection != null && _connection.State != HubConnectionState.Disconnected)
            {
                await _connection.StopAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            UnregisterLifecycleEvents();
            UnregisterHubMethods();

            if (_connection != null)
            {
                await _connection.DisposeAsync().AsTask();
                _connection = null;
            }
        }


        // InvokeAsync  等待服务器响应  有返回值  同步模式
        // SendAsync    不等待响应      无返回值  异步模式

        public async Task<Response> SendIce(string callId, string ice)
        {
            try
            {
                await _connection!.SendAsync("SendIce", callId, ice);
                return new Response(true);
            }
            catch (Exception e)
            {
                return new Response(false, e.Message);
            }
        }

        public async Task<Response> SendOffer(string callId, string offer)
        {
            try
            {
                await _connection!.SendAsync("SendOffer", callId, offer);
                return new Response(true);
            }
            catch (Exception e)
            {
                return new Response(false, e.Message);
            }
        }

        public async Task<Response> SendAnswer(string callId, string answer)
        {
            try
            {
                await _connection!.SendAsync("SendAnswer", callId, answer);
                return new Response(true);
            }
            catch (Exception e)
            {
                return new Response(false, e.Message);
            }
        }




        private HubConnection BuildConnection()
        {
            var connection = new HubConnectionBuilder()
            .WithKeepAliveInterval(TimeSpan.FromSeconds(ApiOptions.KeepAlive))
            .WithServerTimeout(TimeSpan.FromSeconds(ApiOptions.ServerTimeout))
            .WithUrl(ApiUrl.ChatHub, options =>
            {
                //options.CloseTimeout = TimeSpan.FromSeconds(ApiOptions.Timeout);

                // 跳过 HTTPS 证书验证
                //options.HttpMessageHandlerFactory = _ =>
                //new HttpClientHandler
                //{
                //    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                //};

                // 使用 TokenProvider 获取访问令牌
                options.AccessTokenProvider = async () => await _tokenProvider.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect(
            [
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(3),
            ])
            .Build();
            return connection;
        }

        private void RegisterLifecycleEvents()
        {
            _connection?.Reconnecting += OnReconnecting;
            _connection?.Reconnected += OnReconnected;
            _connection?.Closed += OnClosed;
        }

        private void RegisterHubMethods()
        {
            _connection?.On<long>("UserOnline", OnUserOnline);
            _connection?.On<long>("UserOffline", OnUserOffline);

            _connection?.On<long>("ReadAll", OnReadAll);

            _connection?.On<MessageDto, bool>("ReceiveMessage", OnReceiveMessage);
            _connection?.On<MessageDto>("RecallMessage", OnRecallMessage);
            _connection?.On<MessageDto>("DeleteMessage", OnDeleteMessage);

            _connection?.On<ReceiveCallRequest>("ReceiveCall", OnReceiveCall);
            _connection?.On<string>("CallCanceled", OnCallCanceled);
            _connection?.On<string, DateTime>("CallAccepted", OnCallAccepted);
            _connection?.On<string>("CallRejected", OnCallRejected);
            _connection?.On<string>("CallAbnormal", OnCallAbnormal);
            _connection?.On<string>("CallHangUp", OnCallHangUp);
            _connection?.On<string>("CallHandled", OnCallHandled);
            _connection?.On<string>("CallExpiry", OnCallExpiry);

            _connection?.On<string>("ReceiveIce", OnReceiveIce);
            _connection?.On<string>("ReceiveOffer", OnReceiveOffer);
            _connection?.On<string>("ReceiveAnswer", OnReceiveAnswer);

            _connection?.On<ContactRequestDto, ContactDto?, ChatDto?, MessageDto?, bool>("RequestContact", OnRequestContact);
            _connection?.On<ContactRequestDto, ContactDto?, ChatDto?, MessageDto?>("RespondContact", OnRespondContact);
            _connection?.On<long>("DeleteContact", OnDeleteContact);

            _connection?.On<string>("UpdateHead", OnUpdateHead);
            _connection?.On<UpdateUserRequest>("UpdateProfile", OnUpdateProfile);
            _connection?.On<Guid, double>("UploadProgress", OnUploadProgress);

            _connection?.On<string>("ForceLogout", OnForceLogout);
        }


        private void UnregisterLifecycleEvents()
        {
            _connection?.Reconnecting -= OnReconnecting;
            _connection?.Reconnected -= OnReconnected;
            _connection?.Closed -= OnClosed;
        }

        private void UnregisterHubMethods()
        {
            _connection?.Remove("UserOnline");
            _connection?.Remove("UserOffline");

            _connection?.Remove("ReadAll");

            _connection?.Remove("ReceiveMessage");
            _connection?.Remove("RecallMessage");
            _connection?.Remove("DeleteMessage");

            _connection?.Remove("ReceiveCall");
            _connection?.Remove("CallCanceled");
            _connection?.Remove("CallAccepted");
            _connection?.Remove("CallRejected");
            _connection?.Remove("CallAbnormal");
            _connection?.Remove("CallHangUp");
            _connection?.Remove("CallHandled");
            _connection?.Remove("CallExpiry");

            _connection?.Remove("ReceiveIce");
            _connection?.Remove("ReceiveOffer");
            _connection?.Remove("ReceiveAnswer");

            _connection?.Remove("RequestContact");
            _connection?.Remove("RespondContact");
            _connection?.Remove("DeleteContact");

            _connection?.Remove("UpdateHead");
            _connection?.Remove("UpdateProfile");

            _connection?.Remove("ForceLogout");
        }



        #region LifecycleEvents

        public event Action<Exception?> OnReconnecting_ChatHub;
        public event Action<string?> OnReconnected_ChatHub;
        public event Action<Exception?> OnClosed_ChatHub;

        private async Task OnReconnecting(Exception? exception)
        {
            UI.Run(() =>
            {
                // 正在重新连接中
                OnReconnecting_ChatHub?.Invoke(exception);
            });

            await Task.CompletedTask;
        }

        private async Task OnReconnected(string? connectionId)
        {
            UI.Run(() =>
            {
                // 已重新用新 Token 连上
                OnReconnected_ChatHub?.Invoke(connectionId);
            });

            await Task.CompletedTask;
        }

        private async Task OnClosed(Exception? exception)
        {
            await DisposeAsync().AsTask();

            UI.Run(() =>
            {
                OnClosed_ChatHub?.Invoke(exception);
            });
        }

        #endregion





        #region HubMethods

        public event Func<MessageDto, Task<bool>> OnReceiveMessage_ChatHub;
        public event Action<Guid, double> OnUploadProgress_ChatHub;

        private void OnUserOnline(long userId)
        {
            var currentUser = _globalCache.GetCurrentUser();
            if (currentUser.Id == userId) return;

            var contact = _contactStore.GetContact(userId);
            if (contact != null)
            {
                contact.Is_Online = true;
            }
        }

        private void OnUserOffline(long userId)
        {
            var currentUser = _globalCache.GetCurrentUser();
            if (currentUser.Id == userId) return;

            var contact = _contactStore.GetContact(userId);
            if (contact != null)
            {
                contact.Is_Online = false;
            }
        }

        private void OnReadAll(long chatId)
        {
            var chat = _chatStore.GetChat(chatId);
            if (chat != null) chat.Unread_Count = 0;
        }

        private async Task<bool> OnReceiveMessage(MessageDto messageDto)
        {
            var currentUser = _globalCache.GetCurrentUser();
            MessageVM? messageVM = null;

            var chat = _chatStore.GetChat(currentUser.Id, messageDto);
            if (chat != null && chat.Type == ChatType.Private)
            {
                // 私聊 从联系人列表获取发送者信息

                var contact = _contactStore.GetContact(messageDto.Sender_Id);
                messageVM = messageDto.ToVM(currentUser.Id, contact?.Avatar, contact?.Nickname);
                _messageStore.Add(chat.Id, messageVM);

                chat.Last_Msg = messageVM;
                chat.Unread_Count += messageVM.IsSelf ? 0 : 1;
                chat.Is_Deleted = false;
            }
            else if (chat != null && chat.Type == ChatType.Group)
            {
                // 群聊 从群成员列表获取发送者信息

            }
            else
            {
                return false;
            }

            bool result = false;

            var handlers = OnReceiveMessage_ChatHub?.GetInvocationList() ?? [];
            if (handlers.Length > 0)
            {
                result = await UI.Run(async () =>
                {
                    bool handled = false;

                    foreach (var handler in handlers)
                    {
                        if (handler is Func<MessageDto, Task<bool>> func)
                        {
                            handled |= await func(messageDto);
                        }
                    }

                    return handled;
                });
            }

            // 不是自己发的消息且未读
            if (!messageVM.IsSelf && !result)
            {
#if ANDROID || IOS
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(250));
#endif
                try
                {
                    _notificationManager.SendNotification(messageVM.Sender_Nickname, messageVM.Msg_Brief);
                }
                catch { }
            }

            return result;
        }

        private void OnRecallMessage(MessageDto messageDto)
        {
            var currentUser = _globalCache.GetCurrentUser();
            var chat = _chatStore.GetChat(currentUser.Id, messageDto);
            if (chat != null)
            {
                var message = _messageStore.GetMessages(chat.Id).FirstOrDefault(m => m.Id == messageDto.Id);
                if (message != null)
                {
                    message.Message_Status = MessageStatus.Recalled;
                }
                if (chat.Last_Msg?.Id == messageDto.Id)
                {
                    chat.Last_Msg = message;
                    chat.Is_Deleted = false;
                }
            }
        }

        private void OnDeleteMessage(MessageDto messageDto)
        {
            var currentUser = _globalCache.GetCurrentUser();
            var chat = _chatStore.GetChat(currentUser.Id, messageDto);
            if (chat != null)
            {
                var message = _messageStore.GetMessages(chat.Id).FirstOrDefault(m => m.Id == messageDto.Id);
                if (message != null)
                {
                    message.Message_Status = MessageStatus.Deleted;
                }
                if (chat.Last_Msg?.Id == messageDto.Id)
                {
                    chat.Last_Msg = message;
                    chat.Is_Deleted = false;
                }
            }
        }

        private async Task<bool> OnRequestContact(ContactRequestDto contactRequestDto, ContactDto? contactDto, ChatDto? chatDto, MessageDto? messageDto)
        {
            var currentUser = _globalCache.GetCurrentUser();

            _contactRequestStore.Upsert(contactRequestDto.ToVM(currentUser.Id));

            if (contactRequestDto.Relation_Request_Status == RelationRequestStatus.Accepted)
            {
                _contactStore.Upsert(contactDto!.ToVM());

                _chatStore.UpsertAndSetTop(chatDto!.ToVM());

                _messageStore.Add(chatDto!.Id, messageDto!.ToVM(currentUser.Id, contactDto?.Avatar, contactDto?.Nickname));

                if (OnReceiveMessage_ChatHub == null) return false;

                return await UI.Run(async () => await OnReceiveMessage_ChatHub.Invoke(messageDto!));
            }

            return false;
        }

        private void OnRespondContact(ContactRequestDto contactRequestDto, ContactDto? contactDto, ChatDto? chatDto, MessageDto? messageDto)
        {
            var currentUser = _globalCache.GetCurrentUser();

            _contactRequestStore.Upsert(contactRequestDto.ToVM(currentUser.Id));

            if (contactRequestDto.Relation_Request_Status == RelationRequestStatus.Accepted)
            {
                _contactStore.Upsert(contactDto!.ToVM());

                _chatStore.UpsertAndSetTop(chatDto!.ToVM());

                _messageStore.Add(chatDto!.Id, messageDto!.ToVM(currentUser.Id, contactDto?.Avatar, contactDto?.Nickname));
            }
        }

        private void OnDeleteContact(long contactId)
        {
            _chatStore.Remove(ChatType.Private, contactId);
            _contactStore.Remove(contactId);
        }

        private async void OnUpdateHead(string url)
        {
            var currentUser = _globalCache.GetCurrentUser();
            currentUser.Avatar = url;

            _globalCache.UpdateCurrentUser(currentUser);

            await _loginService.SetCurrentUser(currentUser);

            _messageStore.UpdateUserHead(currentUser.Id, url);
        }

        private async void OnUpdateProfile(UpdateUserRequest request)
        {
            var currentUser = _globalCache.GetCurrentUser();
            currentUser.Nickname = request.Nickname;
            currentUser.Phone = request.Phone;
            currentUser.Email = request.Email;

            _globalCache.UpdateCurrentUser(currentUser);

            await _loginService.SetCurrentUser(currentUser);
        }

        private void OnUploadProgress(Guid guid, double progress)
        {
            UI.Run(() => OnUploadProgress_ChatHub?.Invoke(guid, progress));
        }

        private async void OnForceLogout(string message)
        {
            await StopAsync();
        }

        #endregion



        #region Call

        public event Action<ReceiveCallRequest> OnReceiveCall_ChatHub;
        public event Action<string> OnCallCanceled_ChatHub;
        public event Action<string, DateTime> OnCallAccepted_ChatHub;
        public event Action<string> OnCallRejected_ChatHub;
        public event Action<string> OnCallAbnormal_ChatHub;
        public event Action<string> OnCallHangUp_ChatHub;
        public event Action<string> OnCallHandled_ChatHub;
        public event Action<string> OnCallExpiry_ChatHub;

        private void OnReceiveCall(ReceiveCallRequest request)
        {
            UI.Run(() => OnReceiveCall_ChatHub?.Invoke(request));
        }

        private void OnCallCanceled(string callId)
        {
            UI.Run(() => OnCallCanceled_ChatHub?.Invoke(callId));
        }

        private void OnCallAccepted(string callId, DateTime start)
        {
            UI.Run(() => OnCallAccepted_ChatHub?.Invoke(callId, start));
        }

        private void OnCallRejected(string callId)
        {
            UI.Run(() => OnCallRejected_ChatHub?.Invoke(callId));
        }

        private void OnCallAbnormal(string callId)
        {
            UI.Run(() => OnCallAbnormal_ChatHub?.Invoke(callId));
        }

        private void OnCallHangUp(string callId)
        {
            UI.Run(() => OnCallHangUp_ChatHub?.Invoke(callId));
        }

        private void OnCallHandled(string callId)
        {
            UI.Run(() => OnCallHandled_ChatHub?.Invoke(callId));
        }

        private void OnCallExpiry(string callId)
        {
            UI.Run(() => OnCallExpiry_ChatHub?.Invoke(callId));
        }

        #endregion



        #region RTC信令

        public event Action<string> OnReceiveIce_ChatHub;
        public event Action<string> OnReceiveOffer_ChatHub;
        public event Action<string> OnReceiveAnswer_ChatHub;

        private void OnReceiveIce(string ice)
        {
            OnReceiveIce_ChatHub?.Invoke(ice);
        }

        private void OnReceiveOffer(string offer)
        {
            OnReceiveOffer_ChatHub?.Invoke(offer);
        }

        private void OnReceiveAnswer(string answer)
        {
            OnReceiveAnswer_ChatHub?.Invoke(answer);
        }

        #endregion
    }
}
