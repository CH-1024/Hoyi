using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Mapping;
using HY.MAUI.Models;
using HY.MAUI.Services;
using HY.MAUI.Stores;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.PageModels.Contact
{
    public partial class StrangerDetailPageModel : ObservableObject, IQueryAttributable
    {
        int _source = 0;

        private readonly IServiceProvider _serviceProvider;
        private readonly IGlobalCache _globalCache;
        private readonly ContactApi _contactApi;

        private readonly GlobalBinding _globalBinding;

        private readonly ContactRequestStore _contactRequestStore;
        private readonly ContactStore _contactStore;
        private readonly ChatStore _chatStore;
        private readonly MessageStore _messageStore;

        private readonly ChatHubSignalR _chatHub;

        private ContactVM strangerInfo = null;
        public ContactVM StrangerInfo
        {
            get { return strangerInfo; }
            set { SetProperty(ref strangerInfo, value); }
        }



        public StrangerDetailPageModel(IServiceProvider serviceProvider, IGlobalCache globalCache, ContactApi contactApi, GlobalBinding globalBinding,
                                       ContactRequestStore contactRequestStore, ContactStore contactStore, ChatStore chatStore, MessageStore messageStore, ChatHubSignalR chatHub)
        {
            _serviceProvider = serviceProvider;
            _globalCache = globalCache;
            _contactApi = contactApi;

            _globalBinding = globalBinding;

            _contactRequestStore = contactRequestStore;
            _contactStore = contactStore;
            _chatStore = chatStore;
            _messageStore = messageStore;

            _chatHub = chatHub;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            _source = (int)query["Source"];
            StrangerInfo = (ContactVM)query["ContactInfo"];
        }

        [RelayCommand]
        async Task GoBack()
        {
            await Shell.Current.GoToAsync("..", true);
        }


        [RelayCommand]
        async Task RequestContact()
        {
            var currentUser = _globalCache.GetCurrentUser();

            if (currentUser.Id == StrangerInfo.Contact_Id)
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", "这是你自己哦！", "确定");
                return;
            }

            var sayHi = await Application.Current!.Windows[0].Page!.DisplayPromptAsync("发送好友请求", null, "确定", "取消", "打个招呼吧~", 30);
            if (sayHi == null) return;

            var result = await _contactApi.RequestContact(StrangerInfo.Contact_Id, _source, sayHi);
            if (result?.IsSucc != true)
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", $"请求失败[{result?.Msg}]", "确定");
                return;
            }

            var contactRequestDto = result.GetValue<ContactRequestDto>("ContactRequest")!;
            var contactDto = result.GetValue<ContactDto>("Contact");
            var chatDto = result.GetValue<ChatDto>("Chat");
            var messageDto = result.GetValue<MessageDto>("Message");

            _contactRequestStore.Upsert(contactRequestDto.ToVM(currentUser.Id));

            // 更新全局未读消息数
            _globalBinding.RequestContactCount = _contactRequestStore.GetRequestCount();

            if (contactRequestDto.Relation_Request_Status == RelationRequestStatus.Accepted)
            {
                _contactStore.Upsert(contactDto!.ToVM());

                _chatStore.UpsertAndSetTop(chatDto!.ToVM());

                _messageStore.Add(chatDto!.Id, messageDto!.ToVM(currentUser.Id, contactDto!.Avatar, contactDto!.Nickname));

                // 更新全局未读消息数
                _globalBinding.UnreadMessageCount = _chatStore.GetUnreadCount();

                await Shell.Current.GoToAsync("..", true);
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", "你们已经成为好友了", "确定");
            }
            else
            {
                await Shell.Current.GoToAsync("..", true);
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", "请求已发送", "确定");
            }

        }

    }

}
