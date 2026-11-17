using HY.MAUI.Communication.Http;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
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
    public class SyncEventSyncService : ISyncEventSyncService
    {
        private const int PageSize = 100;

        private readonly IGlobalCache _globalCache;
        private readonly IGlobalMapper _globalMapper;
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

        public SyncEventSyncService(GlobalBinding globalBinding, IGlobalCache globalCache, IGlobalMapper globalMapper, ILoginService loginService, 
                                    SyncApi syncApi, UserApi userApi, ContactApi contactApi, ChatApi chatApi, /*SyncCursorStore cursorStore, 
*/                                  ChatStore chatStore, ContactStore contactStore, ContactRequestStore contactRequestStore, MessageStore messageStore)
        {
            _globalBinding = globalBinding;
            _globalCache = globalCache;
            _globalMapper = globalMapper;
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

                    var syncEvents = response.GetValue<List<SyncEventDto>>("Events") ?? [];
                    if (syncEvents.Count == 0) return true;

                    var orderedEvents = syncEvents.OrderBy(x => x.Id).ToList();

                    // First determine which snapshots are invalidated by this event page.
                    var refreshContacts = false;
                    var refreshRequests = false;
                    var refreshChats = false;
                    var refreshProfile = false;

                    foreach (var syncEvent in orderedEvents)
                    {
                        switch (syncEvent.Event_Type)
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
                    foreach (var syncEvent in orderedEvents)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        switch (syncEvent.Event_Type)
                        {
                            case SyncEventTypes.MessageCreated:
                                if (!await ApplyMessageCreatedAsync(user.Id, syncEvent)) return false;
                                break;

                            case SyncEventTypes.MessageRecalled:
                                await ApplyMessageStatusAsync(syncEvent.Entity_Id, MessageStatus.Recalled);
                                break;

                            case SyncEventTypes.MessageDeleted:
                                await ApplyMessageStatusAsync(syncEvent.Entity_Id, MessageStatus.Deleted);
                                break;

                            case SyncEventTypes.ChatRead:
                                await ApplyChatReadAsync(syncEvent.Entity_Id);
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

        private async Task<bool> ApplyMessageCreatedAsync(long currentUserId, SyncEventDto syncEvent)
        {
            if (syncEvent.Related_Id <= 0 || syncEvent.Entity_Id <= 0)
                return true;

            var response = await _syncApi.GetMessage(syncEvent.Related_Id, syncEvent.Entity_Id);
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
                var chatVM = _chatStore.GetChat(syncEvent.Related_Id);
                if (chatVM == null) return;

                if (chatVM.Type == ChatType.Private)
                {
                    // 私聊 从联系人列表获取发送者信息

                    var messageVM = _globalMapper.ToVM(messageDto);
                    _messageStore.AddIfNotExists(chatVM.Id, messageVM);

                    if (chatVM.Last_Msg == null || messageDto.Id >= chatVM.Last_Msg.Id)
                    {
                        chatVM.Last_Msg = messageVM;
                        chatVM.Is_Deleted = false;

                        _chatStore.SetTop(chatVM.Id);
                    }
                }
                else if (chatVM.Type == ChatType.Group)
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
                var messageVM = _messageStore.GetMessage(messageId);
                if (messageVM != null) messageVM.Message_Status = status;
            });
        }

        private async Task ApplyChatReadAsync(long chatId)
        {
            await UI.RunTask(() =>
            {
                var chatVM = _chatStore.GetChat(chatId);
                if (chatVM != null) chatVM.Unread_Count = 0;
            });
        }

        private async Task<bool> RefreshContactsAsync()
        {
            var response = await _contactApi.GetContacts();
            if (response?.IsSucc != true) return false;

            var contactDtos = response.GetValue<List<ContactDto>>("Contacts") ?? [];
            var contactVMs = contactDtos.Select(_globalMapper.ToVM).ToList();

            await UI.RunTask(() => _contactStore.ReplaceSnapshot(contactVMs));

            return true;
        }

        private async Task<bool> RefreshContactRequestsAsync(long userId)
        {
            var response = await _contactApi.GetContactRequests();
            if (response?.IsSucc != true)
                return false;

            var contactRequestDtos = response.GetValue<List<ContactRequestDto>>("ContactRequests") ?? [];
            var contactRequestVMs = contactRequestDtos.Select(_globalMapper.ToVM).ToList();

            await UI.RunTask(() => _contactRequestStore.ReplaceSnapshot(contactRequestVMs));

            return true;
        }

        private async Task<bool> RefreshChatsAsync()
        {
            var response = await _chatApi.GetChats();
            if (response?.IsSucc != true)
                return false;

            var chatDtos = response.GetValue<List<ChatDto>>("Chats") ?? [];
            var chatVMs = chatDtos.Select(_globalMapper.ToVM).ToList();

            await UI.RunTask(() =>
            {
                // Merge last-message references on the UI thread because MessageStore
                // contains ObservableCollections which may be updated by live SignalR events.
                for (var i = 0; i < chatVMs.Count; i++)
                {
                    var lastMessageId = chatDtos[i].Last_Msg?.Id;
                    if (lastMessageId is long id)
                    {
                        var message = _messageStore.GetMessage(id);
                        if (message != null) chatVMs[i].Last_Msg = message;
                    }
                }

                var currentIds = _chatStore.Chats.Select(x => x.Id).ToHashSet();
                var incomingIds = chatVMs.Select(x => x.Id).ToHashSet();

                foreach (var removedChatId in currentIds.Except(incomingIds))
                    _messageStore.Remove(removedChatId);

                _chatStore.ReplaceSnapshot(chatVMs);
            });

            return true;
        }

        private async Task<bool> RefreshCurrentUserAsync()
        {
            var response = await _userApi.GetCurrentUser();
            if (response?.IsSucc != true || response.GetValue<UserDto>("User") is not UserDto userDto) return false;

            var userVM = _globalMapper.ToVM(userDto);

            await _loginService.SetCurrentUser(userVM);

            _globalCache.UpdateCurrentUser(userVM);

            return true;
        }
    }
}
