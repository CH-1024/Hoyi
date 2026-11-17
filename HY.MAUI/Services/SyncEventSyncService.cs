using HY.MAUI.Communication.Http;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Mapping;
using HY.MAUI.Models;
using HY.MAUI.Stores;
using HY.MAUI.Tools;
using System.Diagnostics;

namespace HY.MAUI.Services
{
    /// <summary>
    /// Replays the durable per-user event stream. A page cursor is persisted only after
    /// the required snapshots and all events in that page have been applied successfully.
    /// </summary>
    public sealed class SyncEventSyncService
    {
        private const int PageSize = 100;

        private readonly IGlobalCache _globalCache;
        private readonly ILoginService _loginService;

        private readonly SyncApi _syncApi;
        private readonly UserApi _userApi;
        private readonly ContactApi _contactApi;
        private readonly ChatApi _chatApi;

        //private readonly SyncCursorStore _cursorStore;
        private readonly ChatStore _chatStore;
        private readonly ContactStore _contactStore;
        private readonly ContactRequestStore _contactRequestStore;
        private readonly MessageStore _messageStore;

        private readonly GlobalBinding _globalBinding;

        private readonly SemaphoreSlim _syncLock = new(1, 1);

        public SyncEventSyncService(IGlobalCache globalCache, ILoginService loginService, 
                                    SyncApi syncApi, UserApi userApi, ContactApi contactApi, ChatApi chatApi, SyncCursorStore cursorStore, 
                                    ChatStore chatStore, ContactStore contactStore, ContactRequestStore contactRequestStore, MessageStore messageStore, GlobalBinding globalBinding)
        {
            _globalCache = globalCache;
            _loginService = loginService;

            _syncApi = syncApi;
            _userApi = userApi;
            _contactApi = contactApi;
            _chatApi = chatApi;

            //_cursorStore = cursorStore;
            _chatStore = chatStore;
            _contactStore = contactStore;
            _contactRequestStore = contactRequestStore;
            _messageStore = messageStore;

            _globalBinding = globalBinding;
        }



        /// <summary>
        /// For a new installation/device, capture the event watermark before fetching
        /// contacts, requests, chats and message snapshots. Later changes are replayed.
        /// </summary>
        public async Task<bool> InitCursorAsync()
        {
            var resp = await _syncApi.GetHead();
            if (resp?.IsSucc == true)
            {
                var eventId = resp.GetValue<long>("LastEventId");

                _globalCache.SetLastEventId(eventId);

                return true;
            }

            return false;
        }

        public async Task<bool> SyncAsync(CancellationToken cancellationToken = default)
        {
            await _syncLock.WaitAsync(cancellationToken);

            try
            {
                UserVM user;
                long cursor;
                try
                {
                    user = _globalCache.GetCurrentUser();
                    cursor = _globalCache.GetLastEventId();
                }
                catch
                {
                    return false;
                }

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var response = await _syncApi.GetChanges(cursor, PageSize);
                    if (response?.IsSucc != true) return false;

                    var events = response.GetValue<List<SyncEventDto>>("Events") ?? [];
                    if (events.Count == 0) return true;

                    var orderedEvents = events.OrderBy(x => x.Id).ToList();

                    // First determine which snapshots are invalidated by this event page.
                    var refreshContacts = false;
                    var refreshRequests = false;
                    var refreshChats = false;
                    var refreshProfile = false;

                    foreach (var item in orderedEvents)
                    {
                        switch (item.Event_Type)
                        {
                            case SyncEventTypes.MessageCreated:
                            case SyncEventTypes.MessageRecalled:
                            case SyncEventTypes.MessageDeleted:
                            case SyncEventTypes.ChatChanged:
                                refreshChats = true;
                                break;

                            case SyncEventTypes.ContactsChanged:
                                refreshContacts = true;
                                break;

                            case SyncEventTypes.ContactRequestsChanged:
                                refreshRequests = true;
                                break;

                            case SyncEventTypes.ProfileChanged:
                                refreshProfile = true;
                                break;
                        }

                        if (refreshChats && refreshContacts && refreshRequests && refreshProfile) break;
                    }

                    // New chats/contacts must exist before their related message events are applied.
                    if (refreshProfile && !await RefreshCurrentUserAsync()) return false;
                    if (refreshContacts && !await RefreshContactsAsync()) return false;
                    if (refreshRequests && !await RefreshContactRequestsAsync(user.Id)) return false;
                    if (refreshChats && !await RefreshChatsAsync()) return false;

                    // Apply message and read events in strict per-user sequence order.
                    foreach (var item in orderedEvents)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        switch (item.Event_Type)
                        {
                            case SyncEventTypes.MessageCreated:
                                if (!await ApplyMessageCreatedAsync(user.Id, item)) return false;
                                break;

                            case SyncEventTypes.MessageRecalled:
                                await ApplyMessageStatusAsync(item.Entity_Id, MessageStatus.Recalled);
                                break;

                            case SyncEventTypes.MessageDeleted:
                                await ApplyMessageStatusAsync(item.Entity_Id, MessageStatus.Deleted);
                                break;

                            case SyncEventTypes.ChatRead:
                                await ApplyChatReadAsync(item.Entity_Id);
                                break;
                        }
                    }

                    await UI.RunTask(() =>
                    {
                        _globalBinding.UnreadMessageCount = _chatStore.GetUnreadCount();
                        _globalBinding.RequestContactCount = _contactRequestStore.GetRequestCount();
                        _globalBinding.RespondContactCount = _contactRequestStore.GetRespondCount();
                    });

                    // Persist only after all work in the page has succeeded.
                    cursor = orderedEvents[^1].Id;
                    _globalCache.SetLastEventId(cursor);

                    if (orderedEvents.Count < PageSize) return true;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SyncEventSyncService: {ex}");
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private async Task<bool> ApplyMessageCreatedAsync(long currentUserId, SyncEventDto item)
        {
            if (item.Related_Id <= 0 || item.Entity_Id <= 0)
                return true;

            var response = await _syncApi.GetMessage(item.Related_Id, item.Entity_Id);
            if (response?.IsSucc != true)
            {
                // The message may already have been deleted for this user, or the chat
                // removed before replay. The following delete/chat event or snapshot is final.
                return true;
            }

            var messageDto = response.GetValue<MessageDto>("Message");
            if (messageDto == null) return false;

            await UI.RunTask(() =>
            {
                var chat = _chatStore.GetChat(item.Related_Id);
                if (chat == null) return;

                if (chat.Type == ChatType.Private)
                {
                    // 私聊 从联系人列表获取发送者信息

                    var contact = _contactStore.GetContact(messageDto.Sender_Id);
                    var messageVM = messageDto.ToVM(currentUserId, contact?.Avatar, contact?.Nickname);
                    _messageStore.AddIfNotExists(chat.Id, messageVM);

                    if (chat.Last_Msg == null || messageDto.Id >= chat.Last_Msg.Id)
                    {
                        chat.Last_Msg = messageVM;
                        chat.Is_Deleted = false;

                        _chatStore.SetTop(chat.Id);
                    }
                }
                else if (chat.Type == ChatType.Group)
                {
                    // 群聊 从群成员列表获取发送者信息
                    throw new NotImplementedException("群聊消息处理未实现");
                }
                else
                {
                    return;
                }
            });

            return true;
        }

        private async Task ApplyMessageStatusAsync(long messageId, MessageStatus status)
        {
            await UI.RunTask(() =>
            {
                var message = _messageStore.GetMessage(messageId);
                if (message != null) message.Message_Status = status;
            });
        }

        private async Task ApplyChatReadAsync(long chatId)
        {
            await UI.RunTask(() =>
            {
                var chat = _chatStore.GetChat(chatId);
                if (chat != null) chat.Unread_Count = 0;
            });
        }

        private async Task<bool> RefreshContactsAsync()
        {
            var response = await _contactApi.GetContacts();
            if (response?.IsSucc != true) return false;

            var dtos = response.GetValue<List<ContactDto>>("Contacts") ?? [];
            var items = dtos.Select(x => x.ToVM()).ToList();

            await UI.RunTask(() => _contactStore.ReplaceSnapshot(items));

            return true;
        }

        private async Task<bool> RefreshContactRequestsAsync(long userId)
        {
            var response = await _contactApi.GetContactRequests();
            if (response?.IsSucc != true)
                return false;

            var dtos = response.GetValue<List<ContactRequestDto>>("ContactRequests") ?? [];
            var items = dtos.Select(x => x.ToVM(userId)).ToList();

            await UI.RunTask(() => _contactRequestStore.ReplaceSnapshot(items));

            return true;
        }

        private async Task<bool> RefreshChatsAsync()
        {
            var response = await _chatApi.GetChats();
            if (response?.IsSucc != true)
                return false;

            var dtos = response.GetValue<List<ChatDto>>("Chats") ?? [];
            var items = dtos.Select(dto => dto.ToVM()).ToList();

            await UI.RunTask(() =>
            {
                // Merge last-message references on the UI thread because MessageStore
                // contains ObservableCollections which may be updated by live SignalR events.
                for (var i = 0; i < items.Count; i++)
                {
                    var lastMessageId = dtos[i].Last_Msg?.Id;
                    if (lastMessageId is long id)
                    {
                        var loadedMessage = _messageStore.GetMessage(id);
                        if (loadedMessage != null) items[i].Last_Msg = loadedMessage;
                    }
                }

                var currentIds = _chatStore.Chats.Select(x => x.Id).ToHashSet();
                var incomingIds = items.Select(x => x.Id).ToHashSet();

                foreach (var removedChatId in currentIds.Except(incomingIds))
                    _messageStore.Remove(removedChatId);

                _chatStore.ReplaceSnapshot(items);
            });

            return true;
        }

        private async Task<bool> RefreshCurrentUserAsync()
        {
            var response = await _userApi.GetCurrentUser();
            if (response?.IsSucc != true || response.GetValue<UserDto>("User") is not UserDto userDto) return false;

            var user = userDto.ToVM();

            await _loginService.SetCurrentUser(user);

            _globalCache.UpdateCurrentUser(user);

            return true;
        }
    }
}
