using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Communication.SignalR.Requests;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Mapping;
using HY.MAUI.Models;
using HY.MAUI.Models.MsgVM;
using HY.MAUI.PageModels.Chat.MessageCommands;
using HY.MAUI.Pages.Chat;
using HY.MAUI.Pages.Contact;
using HY.MAUI.SendQueue;
using HY.MAUI.Stores;
using HY.MAUI.Tools;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Threading.Tasks;

namespace HY.MAUI.PageModels.Chat
{
    public partial class MessagePageModel : ObservableObject, IQueryAttributable
    {
        private bool _isNavigatedTo;
        private readonly IServiceProvider _serviceProvider;
        private readonly IGlobalCache _globalCache;
        private readonly IDispatcher _dispatcher;

        private readonly ChatHubSignalR _chatHub;
        private readonly MessageSendService _messageSendService;

        private readonly ChatStore _chatStore;
        private readonly MessageStore _messageStore;
        private readonly ContactStore _contactStore;

        private readonly ChatApi _chatApi;
        private readonly MessageApi _messageApi;
        private readonly ContactApi _contactApi;
        private readonly FileApi _fileApi;
        private readonly LoginApi _loginApi;

        CollectionView _collectionView = null;
        ChatVM _currentChat = null;
        UserVM _currentUser = null;

        private ContactVM? currentContact = null;
        public ContactVM? CurrentContact
        {
            get { return currentContact; }
            set { SetProperty(ref currentContact, value); }
        }

        private bool isLoading;
        public bool Isloading
        {
            get { return isLoading; }
            set { SetProperty(ref isLoading, value); }
        }

        private bool showUnread;
        public bool ShowUnread
        {
            get { return showUnread; }
            set { SetProperty(ref showUnread, value); }
        }

        private int unreadCount;
        public int UnreadCount
        {
            get { return unreadCount; }
            set { SetProperty(ref unreadCount, value); }
        }


        private string? title;
        public string? Title
        {
            get { return title; }
            set { SetProperty(ref title, value); }
        }

        private string inputText = "";
        public string InputText
        {
            get { return inputText; }
            set { SetProperty(ref inputText, value); }
        }

        private ObservableCollection<MessageVM> messageCollection = null;
        public ObservableCollection<MessageVM> MessageCollection
        {
            get { return messageCollection; }
            set { SetProperty(ref messageCollection, value); }
        }

        public MessagePageModel(IServiceProvider serviceProvider, IGlobalCache globalCache, IDispatcher dispatcher, ChatHubSignalR chatHub, MessageSendService messageSendService,
                                ChatStore chatStore, MessageStore messageStore, ContactStore contactStore,
                                ChatApi chatApi, MessageApi messageApi, ContactApi contactApi, FileApi fileApi, LoginApi loginApi)
        {
            _serviceProvider = serviceProvider;
            _globalCache = globalCache;
            _dispatcher = dispatcher;

            _chatHub = chatHub;
            _messageSendService = messageSendService;

            _chatStore = chatStore;
            _messageStore = messageStore;
            _contactStore = contactStore;

            _chatApi = chatApi;
            _messageApi = messageApi;
            _contactApi = contactApi;
            _fileApi = fileApi;
            _loginApi = loginApi;
        }


        private Task<bool> OnReceiveMessage_ChatHub(MessageDto msgDto)
        {
            // 1. 聊天类型不匹配
            if (msgDto.Chat_Type != _currentChat.Type) return Task.FromResult(false);

            // 2. 判断消息是否属于当前聊天
            bool isCurrentChat = msgDto.Chat_Type switch
            {
                ChatType.Private => (msgDto.Sender_Id == _currentUser.Id && msgDto.Target_Id == _currentChat.Target_Id) || (msgDto.Target_Id == _currentUser.Id && msgDto.Sender_Id == _currentChat.Target_Id),
                ChatType.Group => msgDto.Target_Id == _currentChat.Target_Id,
                _ => false
            };

            if (!isCurrentChat) return Task.FromResult(false);

            // 3. 当前聊天处理消息
            _currentChat.Unread_Count = 0;

            if (_lastVisibleItemIndex + 3 >= MessageCollection.Count)
            {
                UnreadCount = 0;
                ShowUnread = false;

                _dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () =>
                {
                    _collectionView.ScrollTo(MessageCollection.LastOrDefault(), position: ScrollToPosition.End, animate: true);
                });
            }
            else
            {
                UnreadCount++;
                ShowUnread = true;
            }

            return Task.FromResult(true);
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            _currentChat = (ChatVM)query["ChatInfo"];
            _currentUser = _globalCache.GetCurrentUser();
            Title = _currentChat?.Target_Name;
            CurrentContact = _currentChat.Type == ChatType.Private ? _contactStore.GetContact(_currentChat.Target_Id) : null;
        }


        [RelayCommand]
        void NavigatedTo(NavigatedToEventArgs e)
        {
            _isNavigatedTo = true;
        }

        [RelayCommand]
        void NavigatedFrom(NavigatedFromEventArgs e)
        {
            if (e.DestinationPage != null && e.DestinationPage.GetType().FullName == "HY.MAUI.Pages.Chat.ChatPage")
            {
                _isNavigatedTo = false;
            }
        }

        [RelayCommand]
        async Task GoBack()
        {
            await Shell.Current.GoToAsync("..", true);
        }

        [RelayCommand]
        void Appearing()
        {
            if (_isNavigatedTo) return;

            if (_currentChat.Unread_Count != 0)
            {
                _ = Task.Run(async () =>
                {
                    var chat = _currentChat;
                    var resp = await _chatApi.ReadAll(chat.Id);
                    if (resp?.IsSucc == true) chat.Unread_Count = 0;
                });
            }
        }

        [RelayCommand]
        void Disappearing()
        {
            if (_isNavigatedTo) return;

        }

        [RelayCommand]
        void Loaded()
        {
            if (_isNavigatedTo) return;

            // 加快进入页面的速度
            _dispatcher.Dispatch(() =>
            {
                MessageCollection = _messageStore.GetMessages(_currentChat.Id);
                _collectionView?.ScrollTo(MessageCollection.LastOrDefault(), animate: false);
            });

            _chatHub.OnReceiveMessage_ChatHub += OnReceiveMessage_ChatHub;
        }

        [RelayCommand]
        void Unloaded()
        {
            if (_isNavigatedTo) return;

            _chatHub.OnReceiveMessage_ChatHub -= OnReceiveMessage_ChatHub;
        }

        [RelayCommand]
        async Task Refresh()
        {
            var takeCount = 50;
            var resp = await _messageApi.GetMessages(_currentChat.Id, MessageCollection.First().Id, takeCount);
            if (resp?.IsSucc == true)
            {
                var messageDtos = resp.GetValue<List<MessageDto>>("Messages") ?? [];

                if (messageDtos.Count < takeCount) _currentChat.IsMsgEnd = true;

                if (_currentChat.Type == ChatType.Private)
                {
                    foreach (var messageDto in messageDtos)
                    {
                        var contact = _contactStore.GetContact(messageDto.Sender_Id);
                        var messageVM = messageDto.ToVM(_currentUser.Id, contact?.Avatar, contact?.Nickname);
                        _messageStore.Insert(_currentChat.Id, messageVM);
                    }
                }
                else if (_currentChat.Type == ChatType.Group)
                {
                    throw new NotImplementedException("群聊消息加载未实现");
                }
            }
        }

        [RelayCommand]
        void CollectionViewLoaded(CollectionView collectionView)
        {
            if (_isNavigatedTo) return;

            _collectionView = collectionView;
        }

        int _lastVisibleItemIndex;
        [RelayCommand]
        async Task CollectionViewScrolled(ItemsViewScrolledEventArgs args)
        {
            //InputText = $"{args.FirstVisibleItemIndex}";
            _lastVisibleItemIndex = args.LastVisibleItemIndex;

            if (args.VerticalDelta == 0) return;

            if (args.VerticalDelta > 0 && args.LastVisibleItemIndex + UnreadCount >= MessageCollection.Count)
            {
                UnreadCount = 0;
                ShowUnread = false;
            }

            if (args.VerticalDelta < 0 && args.FirstVisibleItemIndex <= 5 && !RefreshCommand.IsRunning && !_currentChat.IsMsgEnd)
            {
                var first = MessageCollection.FirstOrDefault();

                _collectionView.IsEnabled = false;
                await RefreshCommand.ExecuteAsync(null);
                _collectionView.IsEnabled = true;

                _collectionView.ScrollTo(first, position: ScrollToPosition.Center, animate: false);
            }

            await Task.Delay(100);
        }

        double _lastOffset;
        DateTime _lastTime;
        async Task GetScrollSpeed(double verticalDelta, double verticalOffset)
        {
            var now = DateTime.UtcNow;

            if (_lastTime != default)
            {
                var dt = (now - _lastTime).TotalSeconds;

                if (dt > 0)
                {
                    var velocity = Math.Abs(verticalDelta) / dt;

                    // velocity 就是近似的滚动速度
                }
            }

            _lastOffset = verticalOffset;
            _lastTime = now;
        }

        [RelayCommand]
        void SelectionChanged(MessageVM message)
        {
            ;
        }



        [RelayCommand]
        async Task Invocation(MessageCommandInvocation cmd)
        {
            if (cmd?.Command == CommandNames.ContactDetail)
            {
                await ContactDetailCommand.ExecuteAsync(cmd.Message);
            }
            else if (cmd?.Command == CommandNames.DeleteMessage)
            {
                await DeleteMessageCommand.ExecuteAsync(cmd.Message);
            }
            else if (cmd?.Command == CommandNames.RecallMessage)
            {
                await RecallMessageCommand.ExecuteAsync(cmd.Message);
            }
            else if (cmd?.Command == CommandNames.TapImageMessage)
            {
                await TapImageMessageCommand.ExecuteAsync(cmd.Message);
            }
            else if (cmd?.Command == CommandNames.TapVideoMessage)
            {
                await TapVideoMessageCommand.ExecuteAsync(cmd.Message);
            }
            else if (cmd?.Command == CommandNames.TapVideoCallMessage)
            {
                await TapVideoCallMessageCommand.ExecuteAsync(cmd.Message);
            }
            else if (cmd?.Command == CommandNames.DownloadFile)
            {

            }
        }

        [RelayCommand]
        async Task ContactDetail(MessageVM message)
        {
            var parameters = new Dictionary<string, object>();
            if (message.IsSelf)
            {
                //// 查看自己信息
                //parameters.Add("UserInfo", _currentUser);
                //await Shell.Current.GoToAsync(nameof(UserDetailPage), true, parameters);
                return;
            }
            else
            {
                var contact = _contactStore.GetContact(message.Sender_Id);
                if (contact != null)
                {
                    // 是联系人
                    parameters.Add("ContactInfo", contact);
                    await Shell.Current.GoToAsync(nameof(ContactDetailPage), true, parameters);
                }
                else
                {
                    var resp = await _contactApi.GetContact(message.Sender_Id);
                    if (resp?.IsSucc == true)
                    {
                        var contactDto = resp.GetValue<ContactDto>("Contact");

                        if (contactDto.Relation_Status == RelationStatus.Friend)
                        {
                            // 是联系人
                            parameters.Add("ContactInfo", contactDto.ToVM());
                            await Shell.Current.GoToAsync(nameof(ContactDetailPage), true, parameters);
                        }
                        else
                        {
                            // 是陌生人
                            parameters.Add("Source", 1);
                            parameters.Add("ContactInfo", contactDto.ToVM());
                            await Shell.Current.GoToAsync(nameof(StrangerDetailPage), true, parameters);
                        }
                    }

                }
            }
        }

        [RelayCommand]
        async Task DeleteMessage(MessageVM message)
        {
            await _messageApi.DeleteMessage(message);
        }

        [RelayCommand]
        async Task RecallMessage(MessageVM message)
        {
            await _messageApi.RecallMessage(message);
        }

        [RelayCommand]
        async Task TapImageMessage(MessageVM message)
        {
            if (message is ImageMessageVM imageMessage)
            {
                var parameters = new Dictionary<string, object>
                {
                    { "Compressed_Image_Url", imageMessage.Compressed_Image_Url }
                };
                await Shell.Current.GoToAsync(nameof(ImagePreviewPage), false, parameters);
            }
        }

        [RelayCommand]
        async Task TapVideoMessage(MessageVM message)
        {
            if (message is VideoMessageVM videoMessage)
            {
                var parameters = new Dictionary<string, object>
                {
                    { "Original_Video_Url", videoMessage.Original_Video_Url }
                };
                await Shell.Current.GoToAsync(nameof(VideoPreviewPage), false, parameters);
            }
        }

        [RelayCommand]
        async Task TapVideoCallMessage(MessageVM message)
        {
            if (message is CallVideoMessageVM videoCallMessage)
            {
                ;
            }
        }




        [RelayCommand]
        async Task SendText()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;

            var textVM = CreateTextMessageVM();

            var task = CreateSendTask(textVM, null);

            // 先显示
            await HandleMessage(task.Message);

            // 再入队
            var isEnqueued = _messageSendService.Enqueue(task);
            if (!isEnqueued)
            {
                textVM.Message_Status = MessageStatus.Failed;
            }

            // 清空输入框
            InputText = "";
        }

        [RelayCommand]
        async Task SendImage()
        {
            var fileResults = await PickImages();
            if (fileResults.Count == 0) return;

            InputText = "";

            foreach (var file in fileResults)
            {
                var imageVM = CreateImageMessageVM();

                var task = CreateSendTask(imageVM, file);

                // 先显示
                await HandleMessage(task.Message);

                // 再入队
                var isEnqueued = _messageSendService.Enqueue(task);
                if (!isEnqueued)
                {
                    imageVM.Message_Status = MessageStatus.Failed;
                }
            }
        }

        [RelayCommand]
        async Task SendVideo()
        {
            var fileResults = await PickVideos();
            if (fileResults.Count == 0) return;

            InputText = "";

            foreach (var file in fileResults)
            {
                var videoVM = CreateVideoMessageVM();

                var task = CreateSendTask(videoVM, file);

                // 先显示
                await HandleMessage(task.Message);

                // 再入队
                var isEnqueued = _messageSendService.Enqueue(task);
                if (!isEnqueued)
                {
                    videoVM.Message_Status = MessageStatus.Failed;
                }
            }
        }

        [RelayCommand]
        async Task SendVoiceCall()
        {
            var isOnline = await _loginApi.Ping();
            if (!isOnline)
            {
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", "网络连接不可用", "确定");
                return;
            }

            var request = new CreateCallRequest
            {
                CallType = CallType.Voice,
                CalleeId = _currentChat!.Target_Id
            };

            var parameters = new Dictionary<string, object>
            {
                { "CreateCallRequest", request },
                { "CalleeAvatar", _currentChat?.Target_Avatar },
                { "CalleeName", _currentChat?.Target_Name },
            };
            await Shell.Current.GoToAsync(nameof(CallCreatePage), false, parameters);
        }

        [RelayCommand]
        async Task SendVideoCall()
        {
            //var isOnline = await _loginApi.Ping();
            //if (!isOnline)
            //{
            //    await Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", "网络连接不可用", "确定");
            //    return;
            //}

            var request = new CreateCallRequest
            {
                CallType = CallType.Video,
                CalleeId = _currentChat!.Target_Id
            };

            var parameters = new Dictionary<string, object>
            {
                { "CreateCallRequest", request },
                { "CalleeAvatar", _currentChat?.Target_Avatar },
                { "CalleeName", _currentChat?.Target_Name },
            };
            await Shell.Current.GoToAsync(nameof(CallCreatePage), false, parameters);
        }

        [RelayCommand]
        void ShowBottom()
        {
            ShowUnread = false;
            UnreadCount = 0;
            _collectionView.ScrollTo(MessageCollection.LastOrDefault(), position: ScrollToPosition.End, animate: true);
        }




        Task HandleMessage(MessageVM msgVM)
        {
            MessageCollection.Add(msgVM);

            _currentChat.Last_Msg = msgVM;
            _currentChat.Is_Deleted = false;

            _dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () =>
            {
                _collectionView.ScrollTo(MessageCollection.LastOrDefault(), position: ScrollToPosition.End, animate: true);
            });

            return Task.CompletedTask;
        }



        async Task<IReadOnlyList<FileResult>> PickImages()
        {
            var result = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "选择图片",
                FileTypes = FilePickerFileType.Images
            });

            return result?.ToList() ?? [];
        }

        async Task<IReadOnlyList<FileResult>> PickVideos()
        {
            var result = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "选择视频",
                FileTypes = FilePickerFileType.Videos
            });

            return result?.ToList() ?? [];
        }



        SendMessageTask CreateSendTask(MessageVM msgVM, FileResult? file)
        {
            return new SendMessageTask
            {
                Message = msgVM,
                file = file,
                Status = SendTaskStatus.Waiting
            };
        }

        TextMessageVM CreateTextMessageVM()
        {
            return new TextMessageVM
            {
                Chat_Type = _currentChat.Type,
                Sender_Id = _currentUser.Id,
                Sender_Avatar = _currentUser.Avatar,
                Target_Id = _currentChat.Target_Id,
                Created_At = DateTime.UtcNow,
                IsSelf = true,
                Message_Status = MessageStatus.Sending,

                Content = InputText.Trim(),
            };
        }

        ImageMessageVM CreateImageMessageVM()
        {
            return new ImageMessageVM
            {
                Chat_Type = _currentChat.Type,
                Sender_Id = _currentUser.Id,
                Sender_Avatar = _currentUser.Avatar,
                Target_Id = _currentChat.Target_Id,
                Created_At = DateTime.UtcNow,
                IsSelf = true,
                Message_Status = MessageStatus.Sending,
            };
        }

        VideoMessageVM CreateVideoMessageVM()
        {
            return new VideoMessageVM
            {
                Chat_Type = _currentChat.Type,
                Sender_Id = _currentUser.Id,
                Sender_Avatar = _currentUser.Avatar,
                Target_Id = _currentChat.Target_Id,
                Created_At = DateTime.UtcNow,
                IsSelf = true,
                Message_Status = MessageStatus.Sending,
            };
        }

    }
}
