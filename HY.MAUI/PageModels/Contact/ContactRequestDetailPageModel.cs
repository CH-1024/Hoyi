using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication.Http;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Mapping;
using HY.MAUI.Models;
using HY.MAUI.Pages.Contact;
using HY.MAUI.Services;
using HY.MAUI.Stores;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HY.MAUI.PageModels.Contact
{
    public partial class ContactRequestDetailPageModel : ObservableObject, IQueryAttributable
    {
        private readonly IGlobalCache _globalCache;

        private readonly GlobalBinding _globalBinding;

        private readonly ContactApi _contactApi;

        private readonly ChatStore _chatStore;
        private readonly MessageStore _messageStore;
        private readonly ContactStore _contactStore;
        private readonly ContactRequestStore _contactRequestStore;


        private string title = "";
        public string Title
        {
            get { return title; }
            set { SetProperty(ref title, value); }
        }

        private ObservableCollection<ContactRequestVM> contactRequestCollection = null;
        public ObservableCollection<ContactRequestVM> ContactRequestCollection
        {
            get { return contactRequestCollection; }
            set { SetProperty(ref contactRequestCollection, value); }
        }


        public ContactRequestDetailPageModel(IGlobalCache globalCache, GlobalBinding globalBinding, ContactApi contactApi, ChatStore chatStore, MessageStore messageStore, ContactStore contactStore, ContactRequestStore contactRequestStore)
        {
            _globalCache = globalCache;
            _globalBinding = globalBinding;

            _contactApi = contactApi;

            _chatStore = chatStore;
            _messageStore = messageStore;
            _contactStore = contactStore;
            _contactRequestStore = contactRequestStore;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            var tt = (string)query["Type"];
            Title = tt + "列表";

            if (tt == "申请中") ContactRequestCollection = _contactRequestStore.GetRequestCollection();
            else if (tt == "待同意") ContactRequestCollection = _contactRequestStore.GetRespondCollection();
        }



        [RelayCommand]
        async Task RespondContact_Accepted(ContactRequestVM contactRequest)
        {
            var resp = await _contactApi.RespondContact(contactRequest.Id, RespondContactHandle.Accepted);
            if (resp?.IsSucc != true)
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", $"申请失败[{resp?.Msg}]！", "确定");
                return;
            }

            var currentUser = _globalCache.GetCurrentUser();
            var contactRequestDto = resp.GetValue<ContactRequestDto>("ContactRequest")!;
            var contactDto = resp.GetValue<ContactDto>("Contact");
            var chatDto = resp.GetValue<ChatDto>("Chat");
            var messageDto = resp.GetValue<MessageDto>("Message");

            _contactRequestStore.Upsert(contactRequestDto.ToVM(currentUser.Id));

            // 更新全局未读消息数
            _globalBinding.RequestContactCount = _contactRequestStore.GetRequestCount();
            _globalBinding.RespondContactCount = _contactRequestStore.GetRespondCount();

            if (contactRequestDto.Relation_Request_Status == RelationRequestStatus.Accepted)
            {
                _contactStore.Upsert(contactDto!.ToVM());

                _chatStore.UpsertAndSetTop(chatDto!.ToVM());

                _messageStore.Add(chatDto!.Id, messageDto!.ToVM(currentUser.Id, contactDto!.Avatar, contactDto!.Nickname));

                // 更新全局未读消息数
                _globalBinding.UnreadMessageCount = _chatStore.GetUnreadCount();
            }
        }

        [RelayCommand]
        async Task RespondContact_Declined(ContactRequestVM contactRequest)
        {
            var resp = await _contactApi.RespondContact(contactRequest.Id, RespondContactHandle.Declined);
            if (resp?.IsSucc != true)
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", $"申请失败[{resp?.Msg}]！", "确定");
                return;
            }

            var currentUser = _globalCache.GetCurrentUser();
            var contactRequestDto = resp.GetValue<ContactRequestDto>("ContactRequest")!;
            //var contactDto = resp.GetValue<ContactDto>("Contact");
            //var chatDto = resp.GetValue<ChatDto>("Chat");
            //var messageDto = resp.GetValue<MessageDto>("Message");

            _contactRequestStore.Upsert(contactRequestDto.ToVM(currentUser.Id));

            // 更新全局未读消息数
            _globalBinding.RequestContactCount = _contactRequestStore.GetRequestCount();
            _globalBinding.RespondContactCount = _contactRequestStore.GetRespondCount();
        }

        [RelayCommand]
        async Task RespondContact_Revoked(ContactRequestVM contactRequest)
        {
            var resp = await _contactApi.RespondContact(contactRequest.Id, RespondContactHandle.Revoked);
            if (resp?.IsSucc != true)
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", $"申请失败[{resp?.Msg}]！", "确定");
                return;
            }

            var currentUser = _globalCache.GetCurrentUser();
            var contactRequestDto = resp.GetValue<ContactRequestDto>("ContactRequest")!;
            //var contactDto = resp.GetValue<ContactDto>("Contact");
            //var chatDto = resp.GetValue<ChatDto>("Chat");
            //var messageDto = resp.GetValue<MessageDto>("Message");

            _contactRequestStore.Upsert(contactRequestDto.ToVM(currentUser.Id));

            // 更新全局未读消息数
            _globalBinding.RequestContactCount = _contactRequestStore.GetRequestCount();
            _globalBinding.RespondContactCount = _contactRequestStore.GetRespondCount();
        }

        [RelayCommand]
        async Task ContactDetail(ContactRequestVM contactRequest)
        {
            var parameters = new Dictionary<string, object>();

            var contactId = contactRequest.IsSelf ? contactRequest.Receiver_Id : contactRequest.Sender_Id;

            var contact = _contactStore.GetContact(contactId);
            if (contact != null)
            {
                // 是联系人
                parameters.Add("ContactInfo", contact);
                await Shell.Current.GoToAsync(nameof(ContactDetailPage), true, parameters);
            }
            else
            {
                var resp = await _contactApi.GetContact(contactId);
                if (resp?.IsSucc == true)
                {
                    var contactDto = resp.GetValue<ContactDto>("Contact")!;

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
}
