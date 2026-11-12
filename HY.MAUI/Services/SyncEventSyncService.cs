using HY.MAUI.Communication.Http;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Mapping;
using HY.MAUI.Models;
using HY.MAUI.Stores;
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

        private readonly SyncApi _syncApi;
        private readonly UserApi _userApi;
        private readonly ContactApi _contactApi;
        private readonly ChatApi _chatApi;
        private readonly IGlobalCache _globalCache;
        private readonly ILoginService _loginService;
        private readonly SyncCursorStore _cursorStore;
        private readonly ChatStore _chatStore;
        private readonly ContactStore _contactStore;
        private readonly ContactRequestStore _contactRequestStore;
        private readonly MessageStore _messageStore;
        private readonly GlobalBinding _globalBinding;

        private readonly SemaphoreSlim _syncLock = new(1, 1);

        public SyncEventSyncService(
            SyncApi syncApi,
            UserApi userApi,
            ContactApi contactApi,
            ChatApi chatApi,
            IGlobalCache globalCache,
            ILoginService loginService,
            SyncCursorStore cursorStore,
            ChatStore chatStore,
            ContactStore contactStore,
            ContactRequestStore contactRequestStore,
            MessageStore messageStore,
            GlobalBinding globalBinding)
        {
            _syncApi = syncApi;
            _userApi = userApi;
            _contactApi = contactApi;
            _chatApi = chatApi;
            _globalCache = globalCache;
            _loginService = loginService;
            _cursorStore = cursorStore;
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
        public async Task<bool> InitializeCursorAsync()
        {
            UserVM user;
            try
            {
                user = _globalCache.GetCurrentUser();
            }
            catch
            {
                return false;
            }

            if (_cursorStore.TryGet(user.Id, out _))
                return true;

            var response = await _syncApi.GetHead();
            if (response?.IsSucc != true)
                return false;

            _cursorStore.Set(user.Id, response.GetValue<long>("LastEventId"));
            return true;
        }

        public async Task<bool> SyncAsync(CancellationToken cancellationToken = default)
        {
            await _syncLock.WaitAsync(cancellationToken);

            try
            {
                UserVM user;
                try
                {
                    user = _globalCache.GetCurrentUser();
                }
                catch
                {
                    return false;
                }

                if (!_cursorStore.TryGet(user.Id, out var cursor))
                {
                    if (!await InitializeCursorAsync())
                        return false;

                    _cursorStore.TryGet(user.Id, out cursor);
                }

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var response = await _syncApi.GetChanges(cursor, PageSize);
                    if (response?.IsSucc != true)
                        return false;

                    var events = response.GetValue<List<SyncEventDto>>("Events") ?? [];
                    if (events.Count == 0)
                        return true;

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
                    }

                    // New chats/contacts must exist before their related message events are applied.
                    if (refreshProfile && !await RefreshCurrentUserAsync())
                        return false;
                    if (refreshContacts && !await RefreshContactsAsync())
                        return false;
                    if (refreshRequests && !await RefreshContactRequestsAsync(user.Id))
                        return false;
                    if (refreshChats && !await RefreshChatsAsync())
                        return false;

                    // Apply message and read events in strict per-user sequence order.
                    foreach (var item in orderedEvents)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        switch (item.Event_Type)
                        {
                            case SyncEventTypes.MessageCreated:
                                if (!await ApplyMessageCreatedAsync(user.Id, item))
                                    return false;
                                break;

                            case SyncEventTypes.MessageRecalled:
                                await ApplyMessageStatusAsync(item.Entity_Id, MessageStatus.Recalled);
                                break;

                            case SyncEventTypes.MessageDeleted:
                                await ApplyMessageStatusAsync(item.Entity_Id, MessageStatus.Deleted);
                                break;

                            case SyncEventTypes.ChatRead:
                                await MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    var chat = _chatStore.GetChat(item.Entity_Id);
                                    if (chat != null)
                                        chat.Unread_Count = 0;
                                });
                                break;
                        }
                    }

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _globalBinding.UnreadMessageCount = _chatStore.GetUnreadCount();
                        _globalBinding.RequestContactCount = _contactRequestStore.GetRequestCount();
                        _globalBinding.RespondContactCount = _contactRequestStore.GetRespondCount();
                    });

                    // Persist only after all work in the page has succeeded.
                    cursor = orderedEvents[^1].Id;
                    _cursorStore.Set(user.Id, cursor);

                    if (orderedEvents.Count < PageSize)
                        return true;
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

            var dto = response.GetValue<MessageDto>("Message");
            if (dto == null)
                return false;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var chat = _chatStore.GetChat(item.Related_Id);
                if (chat == null)
                    return;

                var contact = dto.Chat_Type == ChatType.Private
                    ? _contactStore.GetContact(dto.Sender_Id)
                    : null;

                var message = dto.ToVM(
                    currentUserId,
                    contact?.Avatar,
                    contact?.Nickname);

                _messageStore.AddIfNotExists(chat.Id, message);

                if (chat.Last_Msg == null || dto.Id >= chat.Last_Msg.Id)
                {
                    chat.Last_Msg = message;
                    chat.Is_Deleted = false;
                    _chatStore.SetTop(chat.Id);
                }
            });

            return true;
        }

        private async Task ApplyMessageStatusAsync(long messageId, MessageStatus status)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var message = _messageStore.GetMessage(messageId);
                if (message != null)
                    message.Message_Status = status;
            });
        }

        private async Task<bool> RefreshContactsAsync()
        {
            var response = await _contactApi.GetContacts();
            if (response?.IsSucc != true)
                return false;

            var dtos = response.GetValue<List<ContactDto>>("Contacts") ?? [];
            var items = dtos.Select(x => x.ToVM()).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
                _contactStore.ReplaceSnapshot(items));

            return true;
        }

        private async Task<bool> RefreshContactRequestsAsync(long userId)
        {
            var response = await _contactApi.GetContactRequests();
            if (response?.IsSucc != true)
                return false;

            var dtos = response.GetValue<List<ContactRequestDto>>("ContactRequests") ?? [];
            var items = dtos.Select(x => x.ToVM(userId)).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
                _contactRequestStore.ReplaceSnapshot(items));

            return true;
        }

        private async Task<bool> RefreshChatsAsync()
        {
            var response = await _chatApi.GetChats();
            if (response?.IsSucc != true)
                return false;

            var dtos = response.GetValue<List<ChatDto>>("Chats") ?? [];
            var items = dtos.Select(dto =>
            {
                var chat = dto.ToVM();
                if (dto.Last_Msg != null)
                {
                    var loadedMessage = _messageStore.GetMessage(dto.Last_Msg.Id);
                    if (loadedMessage != null)
                        chat.Last_Msg = loadedMessage;
                }

                return chat;
            }).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
                _chatStore.ReplaceSnapshot(items));

            return true;
        }

        private async Task<bool> RefreshCurrentUserAsync()
        {
            var response = await _userApi.GetCurrentUser();
            if (response?.IsSucc != true ||
                response.GetValue<UserDto>("User") is not UserDto userDto)
                return false;

            var user = userDto.ToVM();
            await _loginService.SetCurrentUser(user);
            _globalCache.UpdateCurrentUser(user);
            return true;
        }
    }
}
