using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication.Auth;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Communication.SignalR.Requests;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using HY.MAUI.Models.MsgVM;
using HY.MAUI.Pages.Chat;
using HY.MAUI.Pages.Login;
using HY.MAUI.Services;
using HY.MAUI.Stores;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HY.MAUI.PageModels.Login
{
    public partial class LoadingPageModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IGlobalCache _globalCache;
        private readonly IGlobalMapper _globalMapper;
        private readonly ILoginService _loginService;
        private readonly ITokenProvider _tokenProvider;
        private readonly IAuthService _authService;

        private readonly ChatHubSignalR _chatHub;
        private readonly ISyncEventSyncService _syncEventSyncService;

        private readonly UserApi _userApi;
        private readonly ChatApi _chatApi;
        private readonly ContactApi _contactApi;
        private readonly MessageApi _messageApi;
        private readonly SyncApi _syncApi;

        private readonly ChatStore _chatStore;
        private readonly ContactStore _contactStore;
        private readonly ContactRequestStore _contactRequestStore;
        private readonly MessageStore _messageStore;

        private readonly GlobalBinding _globalBinding;



        private UserVM? currentUser;
        public UserVM? CurrentUser
        {
            get { return currentUser; }
            set { SetProperty(ref currentUser, value); }
        }

        private string loadingMessage;
        public string LoadingMessage
        {
            get { return loadingMessage; }
            set { SetProperty(ref loadingMessage, value); }
        }


        public LoadingPageModel(IServiceProvider serviceProvider, IGlobalCache globalCache, IGlobalMapper globalMapper, ILoginService loginService, ITokenProvider tokenProvider,
                                IAuthService authService, ChatHubSignalR chatHub, ISyncEventSyncService syncEventSyncService,
                                UserApi userApi, ChatApi chatApi, ContactApi contactApi, MessageApi messageApi, SyncApi syncApi,
                                ChatStore chatStore, ContactStore contactStore, ContactRequestStore contactRequestStore, MessageStore messageStore,
                                GlobalBinding globalBinding)
        {
            _serviceProvider = serviceProvider;
            _globalCache = globalCache;
            _globalMapper = globalMapper;
            _loginService = loginService;
            _tokenProvider = tokenProvider;
            _authService = authService;

            _chatHub = chatHub;
            _syncEventSyncService = syncEventSyncService;

            _userApi = userApi;
            _chatApi = chatApi;
            _contactApi = contactApi;
            _messageApi = messageApi;
            _syncApi = syncApi;

            _chatStore = chatStore;
            _contactStore = contactStore;
            _contactRequestStore = contactRequestStore;
            _messageStore = messageStore;

            _globalBinding = globalBinding;
        }


        [RelayCommand]
        async Task Appearing()
        {
            if (!await LoadCurrentUser())
            {
                _loginService.Logout();
                _tokenProvider.Clear();

                GoBackToLoginPage();
                return;
            }
        }

        [RelayCommand]
        async Task Loaded()
        {
            // 检查访问令牌是否过期，过期自动刷新令牌
            if (await _tokenProvider.IsAccessTokenExpired() && !await _authService.RefreshTokenAsync())
            {
                _loginService.Logout();
                _tokenProvider.Clear();

                GoBackToLoginPage();
                return;
            }

            LoadingMessage = "正在加载用户信息";
            var resp = await _userApi.GetInfo();
            if (resp?.IsSucc != true || resp.GetValue<UserDto>("User") is not UserDto userDto)
            {
                _loginService.Logout();
                _tokenProvider.Clear();

                GoBackToLoginPage();
                return;
            }

            if (!await RefreshCurrentUser(_globalMapper.ToVM(userDto)))
            {
                _loginService.Logout();
                _tokenProvider.Clear();

                GoBackToLoginPage();
                return;
            }

            LoadingMessage = "正在连接服务器";
            if (!await _chatHub.StartAsync())
            {
                _loginService.Logout();
                _tokenProvider.Clear();

                GoBackToLoginPage();
                return;
            }

            _chatHub.OnReconnected_ChatHub += ChatHub_OnReconnected_ChatHub;
            _chatHub.OnClosed_ChatHub += ChatHub_OnClosed_ChatHub;
            _chatHub.OnReceiveCall_ChatHub += OnReceiveCall_ChatHub;

            LoadingMessage = "正在初始化同步游标";
            if (!await _syncEventSyncService.InitCursorAsync())
            {
                _loginService.Logout();
                _tokenProvider.Clear();

                GoBackToLoginPage();
                return;
            }

            LoadingMessage = "正在加载联系人";
            await LoadContact();

            LoadingMessage = "正在加载联系人请求";
            await LoadContactRequest();

            LoadingMessage = "正在加载会话";
            await LoadChats();

            LoadingMessage = "正在加载消息";
            await LoadMessage();

            LoadingMessage = "正在同步数据";
            if (!await _syncEventSyncService.SyncAsync())
            {
                _loginService.Logout();
                _tokenProvider.Clear();

                GoBackToLoginPage();
                return;
            }

            GoToAppShell();
        }

        private async Task<bool> LoadCurrentUser()
        {
            CurrentUser = await _loginService.GetCurrentUser();
            if (CurrentUser != null)
            {
                _globalCache.SetCurrentUser(CurrentUser);
                return true;
            }

            return false;
        }

        private async Task<bool> RefreshCurrentUser(UserVM userVM)
        {
            if (userVM == null) return false;

            await _loginService.SetCurrentUser(userVM);

            CurrentUser = await _loginService.GetCurrentUser();
            if (CurrentUser != null)
            {
                _globalCache.SetCurrentUser(CurrentUser);
                return true;
            }

            return false;
        }

        private async Task LoadContact()
        {
            _contactStore.Clear();

            var resp = await _contactApi.GetContacts();
            if (resp?.IsSucc == true)
            {
                var contactDtos = resp.GetValue<List<ContactDto>>("Contacts") ?? [];
                foreach (var contactDto in contactDtos)
                {
                    _contactStore.Upsert(_globalMapper.ToVM(contactDto));
                }
            }
        }

        private async Task LoadContactRequest()
        {
            _contactRequestStore.Clear();

            var resp = await _contactApi.GetContactRequests();
            if (resp?.IsSucc == true)
            {
                var contactRequestDtos = resp.GetValue<List<ContactRequestDto>>("ContactRequests") ?? [];
                foreach (var contactRequestDto in contactRequestDtos)
                {
                    _contactRequestStore.Upsert(_globalMapper.ToVM(contactRequestDto));
                }

                _globalBinding.RequestContactCount = _contactRequestStore.GetRequestCount();
                _globalBinding.RespondContactCount = _contactRequestStore.GetRespondCount();
            }
        }

        private async Task LoadChats()
        {
            _chatStore.Clear();

            var resp = await _chatApi.GetChats();
            if (resp?.IsSucc == true)
            {
                var chatDtos = resp.GetValue<List<ChatDto>>("Chats") ?? [];
                foreach (var chatDto in chatDtos)
                {
                    _chatStore.Upsert(_globalMapper.ToVM(chatDto));
                }

                _globalBinding.UnreadMessageCount = _chatStore.GetUnreadCount();
            }
        }

        private async Task LoadMessage()
        {
            _messageStore.Clear();

            foreach (var chat in _chatStore.Chats)
            {
                var resp = await _messageApi.GetMessages(chat.Id);
                if (resp?.IsSucc == true)
                {
                    var messageDtos = resp.GetValue<List<MessageDto>>("Messages") ?? [];

                    if (chat.Type == ChatType.Private)
                    {
                        foreach (var messageDto in messageDtos)
                        {
                            var messageVM = _globalMapper.ToVM(messageDto);
                            _messageStore.Insert(chat.Id, messageVM);
                        }
                    }
                    else if (chat.Type == ChatType.Group)
                    {
                        throw new NotImplementedException("群聊消息加载未实现");
                    }
                }
            }
        }

        private void GoBackToLoginPage()
        {
            var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
            Application.Current!.Windows[0].Page = new NavigationPage(loginPage);
        }

        private void GoToAppShell()
        {
            Application.Current!.Windows[0].Page = _serviceProvider.GetRequiredService<AppShell>();

            // #if WINDOWS || MACCATALYST
            //             Application.Current!.Windows[0].Page = _serviceProvider.GetRequiredService<AppShell_Desktop>();
            // #else
            //             Application.Current!.Windows[0].Page = _serviceProvider.GetRequiredService<AppShell_Phone>();
            // #endif
        }



        private async void ChatHub_OnReconnected_ChatHub(string? connectionId)
        {
            await _syncEventSyncService.SyncAsync();
        }

        private void ChatHub_OnClosed_ChatHub(Exception? e)
        {
            _chatHub.OnReconnected_ChatHub -= ChatHub_OnReconnected_ChatHub;
            _chatHub.OnClosed_ChatHub -= ChatHub_OnClosed_ChatHub;
            _chatHub.OnReceiveCall_ChatHub -= OnReceiveCall_ChatHub;

            _loginService.Logout();
            _tokenProvider.Clear();
            GoBackToLoginPage();

            //var error = e?.Message ?? "未知";
            //_ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("异常", $"与服务器的连接已关闭，原因：{error}", "确定");

            if (e != null) _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("异常", $"与服务器的连接已关闭，原因：{e.Message}", "确定");
        }

        private async void OnReceiveCall_ChatHub(ReceiveCallRequest request)
        {
            var contact = _contactStore.GetContact(request.CallerId);
            if (contact == null) return;

            var parameters = new Dictionary<string, object>
            {
                { "ReceiveCallRequest", request },
                { "CallerAvatar", contact.Avatar },
                { "CallerName", contact.Nickname },
            };
            await Shell.Current.GoToAsync(nameof(CallSelectPage), false, parameters);
        }

    }
}
