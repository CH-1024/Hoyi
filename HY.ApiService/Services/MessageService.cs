using HY.ApiService.Dtos;
using HY.ApiService.Entities;
using HY.ApiService.Enums;
using HY.ApiService.Repositories;
using HY.ApiService.Tools;
using Mapster;
using SqlSugar;
using System.Net.NetworkInformation;

namespace HY.ApiService.Services
{
    public interface IMessageService
    {
        // MessageAction
        Task<bool> InsertMessageAction(long userId, long messageId, MessageActionType actiontype);


        // Message
        Task<bool> HandleNewMessage(MessageDto messageDto);

        Task<long> InsertMessage(MessageDto messageDto);

        Task<MessageDto?> GetMessageById(long currentUserId, long messageId);
        Task<MessageDto?> GetMessageForSync(long currentUserId, long chatId, long messageId);
        Task<List<MessageDto>> GetMessagesByChatId(long chatId, long skipMessageId, int take);
        Task<List<MessageDto>> GetMessagesAfter(long currentUserId, long chatId, long afterMessageId, int take);
        Task<List<MessageDto>> GetPrivateChatMessages(long currentUserId, long targetUserId, long skipMessageId, int take);
        Task<List<MessageDto>> GetGroupChatMessages(long currentUserId, long groupId, long skipMessageId, int take);

        Task<bool> RecallMessage(long messageId);
    }

    public class MessageService : IMessageService
    {
        private readonly ISqlSugarClient _db;

        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IMessageActionRepository _messageActionRepository;
        private readonly IGroupMemberRepository _groupMemberRepository;
        private readonly ISyncEventRepository _syncEventRepository;

        public MessageService(ISqlSugarClient db, IUserRepository userRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IMessageActionRepository messageActionRepository, IGroupMemberRepository groupMemberRepository, ISyncEventRepository syncEventRepository)
        {
            _db = db;
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _messageRepository = messageRepository;
            _messageActionRepository = messageActionRepository;
            _groupMemberRepository = groupMemberRepository;
            _syncEventRepository = syncEventRepository;
        }


        // MessageAction
        public async Task<bool> InsertMessageAction(long userId, long messageId, MessageActionType actiontype)
        {
            var messageEntity = await _messageRepository.GetMessageByIdWithoutFilter(messageId);
            if (messageEntity == null)
                return false;

            var messageActionEntity = new MessageActionEntity
            {
                User_Id = userId,
                Message_Id = messageId,
                Action_Type = actiontype,
                Created_At = DateTime.UtcNow,
            };

            var result = await _db.Ado.UseTranAsync(async () =>
            {
                var inserted = await _messageActionRepository.InsertMessageAction(messageActionEntity);
                if (!inserted)
                    throw new Exception("保存消息操作失败");

                if (actiontype == MessageActionType.Delete)
                {
                    var targetId = messageEntity.Chat_Type == ChatType.Private
                        ? (messageEntity.Sender_Id == userId ? messageEntity.Target_Id : messageEntity.Sender_Id)
                        : messageEntity.Target_Id;

                    var chat = await _chatRepository.GetChatByUserIdAndType(
                        userId, targetId, messageEntity.Chat_Type);

                    var events = new List<SyncEventEntity>
                    {
                        SyncEventFactory.Create(
                            userId,
                            SyncEventTypes.MessageDeleted,
                            messageId,
                            chat?.Id ?? 0)
                    };

                    if (chat != null)
                    {
                        events.Add(SyncEventFactory.Create(
                            userId,
                            SyncEventTypes.ChatChanged,
                            chat.Id));
                    }

                    if (!await _syncEventRepository.InsertSyncEventsAsync(events))
                        throw new Exception("写入消息删除同步事件失败");
                }
            });

            return result.IsSuccess;
        }



        // Message
        public async Task<bool> HandleNewMessage(MessageDto messageDto)
        {
            MessageEntity? messageEntity = null;

            // 开启事务
            var result = await _db.Ado.UseTranAsync(async () =>
            {
                // 保存消息
                messageEntity = messageDto.Adapt<MessageEntity>();
                messageEntity.Message_Status = MessageStatus.Sented;
                messageEntity.Created_At = DateTime.UtcNow;
                messageDto.Id = await _messageRepository.InsertMessage(messageEntity);
                if (messageDto.Id == 0) throw new Exception("保存消息失败");

                // 更新会话的最后一条消息
                if (messageDto.Chat_Type == ChatType.Private)
                {
                    // 单人

                    var senderChatEntity = await _chatRepository.GetChatByUserIdAndType(messageDto.Sender_Id, messageDto.Target_Id, ChatType.Private);
                    if (senderChatEntity != null)
                    {
                        senderChatEntity.Is_Deleted = false;
                        senderChatEntity.Last_Msg_Id = messageDto.Id;
                        //senderChatEntity.Unread_Count = 0;
                        senderChatEntity.Last_Msg_Time = messageDto.Created_At;
                        var bol = await _chatRepository.UpdateChat(senderChatEntity);
                        if (!bol) throw new Exception("更新发送者聊天记录失败");
                    }

                    var receiverChatEntity = await _chatRepository.GetChatByUserIdAndType(messageDto.Target_Id, messageDto.Sender_Id, ChatType.Private);
                    if (receiverChatEntity != null)
                    {
                        receiverChatEntity.Is_Deleted = false;
                        receiverChatEntity.Last_Msg_Id = messageDto.Id;
                        receiverChatEntity.Unread_Count += 1;
                        receiverChatEntity.Last_Msg_Time = messageDto.Created_At;
                        var bol = await _chatRepository.UpdateChat(receiverChatEntity);
                        if (!bol) throw new Exception("更新接收者聊天记录失败");
                    }
                }
                else if (messageDto.Chat_Type == ChatType.Group)
                {
                    // 群聊

                    var groupMembers = await _groupMemberRepository.GetGroupMembersByGroupId(messageDto.Target_Id);

                    var userIds = groupMembers.Select(m => m.User_Id).ToList();

                    var memberChatEntities = await _chatRepository.GetChatsByUserIdsAndType(userIds, messageDto.Target_Id, ChatType.Group);
                    foreach (var memberChatEntity in memberChatEntities)
                    {
                        if (memberChatEntity.User_Id == messageDto.Sender_Id)
                        {
                            memberChatEntity.Is_Deleted = false;
                            memberChatEntity.Last_Msg_Id = messageDto.Id;
                            //memberChatEntity.Unread_Count = 0;
                            memberChatEntity.Last_Msg_Time = messageDto.Created_At;
                        }
                        else
                        {
                            memberChatEntity.Is_Deleted = false;
                            memberChatEntity.Last_Msg_Id = messageDto.Id;
                            memberChatEntity.Unread_Count += 1;
                            memberChatEntity.Last_Msg_Time = messageDto.Created_At;
                        }
                    }

                    var bol = await _chatRepository.UpdateChats(memberChatEntities);
                    if (!bol) throw new Exception("更新群成员聊天记录失败");
                }

                // 将消息变更写入每个会话成员的同步事件流。
                List<ChatEntity> eventChats;
                if (messageDto.Chat_Type == ChatType.Private)
                {
                    eventChats = new List<ChatEntity>();

                    var senderChat = await _chatRepository.GetChatByUserIdAndType(
                        messageDto.Sender_Id, messageDto.Target_Id, ChatType.Private);
                    var receiverChat = await _chatRepository.GetChatByUserIdAndType(
                        messageDto.Target_Id, messageDto.Sender_Id, ChatType.Private);

                    if (senderChat != null) eventChats.Add(senderChat);
                    if (receiverChat != null) eventChats.Add(receiverChat);
                }
                else
                {
                    var members = await _groupMemberRepository.GetGroupMembersByGroupId(messageDto.Target_Id);
                    eventChats = await _chatRepository.GetChatsByUserIdsAndType(
                        members.Select(m => m.User_Id).Distinct().ToList(),
                        messageDto.Target_Id,
                        ChatType.Group);
                }

                var syncEvents = new List<SyncEventEntity>();
                foreach (var chat in eventChats)
                {
                    syncEvents.Add(SyncEventFactory.Create(
                        chat.User_Id,
                        SyncEventTypes.MessageCreated,
                        messageDto.Id,
                        chat.Id));
                    syncEvents.Add(SyncEventFactory.Create(
                        chat.User_Id,
                        SyncEventTypes.ChatChanged,
                        chat.Id));
                }

                if (!await _syncEventRepository.InsertSyncEventsAsync(syncEvents))
                    throw new Exception("写入新消息同步事件失败");
            });

            // ---------- 事务结束 ----------
            if (result.IsSuccess)
            {
                // 设置消息状态和创建时间
                messageDto.Message_Status = messageEntity!.Message_Status;
                messageDto.Created_At = messageEntity!.Created_At;
            }

            return result.IsSuccess;
        }


        public async Task<long> InsertMessage(MessageDto messageDto)
        {
            var messageEntity = messageDto.Adapt<MessageEntity>();
            messageDto.Id = await _messageRepository.InsertMessage(messageEntity);
            return messageDto.Id;
        }



        public async Task<MessageDto?> GetMessageById(long currentUserId, long messageId)
        {
            var messageEntity = await _messageRepository.GetMessageById(currentUserId, messageId);
            if (messageEntity == null)
            {
                return null;
            }

            var messageDto = messageEntity.Adapt<MessageDto>();

            return messageDto;
        }

        public async Task<MessageDto?> GetMessageForSync(long currentUserId, long chatId, long messageId)
        {
            var chat = await _chatRepository.GetChatByChatId(chatId);
            if (chat == null || chat.User_Id != currentUserId)
                return null;

            var entity = await _messageRepository.GetMessageForSync(
                currentUserId, chat.Type, chat.Target_Id, messageId);

            return entity?.Adapt<MessageDto>();
        }

        public async Task<List<MessageDto>> GetMessagesByChatId(long chatId, long skipMessageId, int take)
        {
            var chatEntity = await _chatRepository.GetChatByChatId(chatId);

            if (chatEntity?.Type == ChatType.Private)
            {
                return await GetPrivateChatMessages(chatEntity.User_Id, chatEntity.Target_Id, skipMessageId, take);
            }
            else if (chatEntity?.Type == ChatType.Group)
            {
                return await GetGroupChatMessages(chatEntity.User_Id, chatEntity.Target_Id, skipMessageId, take);
            }
            else
            {
                return new List<MessageDto>();
            }
        }

        public async Task<List<MessageDto>> GetMessagesAfter(
            long currentUserId,
            long chatId,
            long afterMessageId,
            int take)
        {
            var chat = await _chatRepository.GetChatByChatId(chatId);
            if (chat == null || chat.User_Id != currentUserId)
                return [];

            var entities = await _messageRepository.GetMessagesAfter(
                currentUserId,
                chat.Type,
                chat.Target_Id,
                afterMessageId,
                take);

            return entities.Adapt<List<MessageDto>>();
        }

        public async Task<List<MessageDto>> GetPrivateChatMessages(long currentUserId, long targetUserId, long skipMessageId, int take)
        {
            var messageEntities = await _messageRepository.GetPrivateChatMessages(currentUserId, targetUserId, skipMessageId, take);

            var messageDtos = messageEntities.Adapt<List<MessageDto>>();

            return messageDtos;
        }

        public async Task<List<MessageDto>> GetGroupChatMessages(long currentUserId, long groupId, long skipMessageId, int take)
        {
            var messageEntities = await _messageRepository.GetGroupChatMessages(currentUserId, groupId, skipMessageId, take);

            var messageDtos = messageEntities.Adapt<List<MessageDto>>();

            return messageDtos;
        }



        public async Task<bool> RecallMessage(long messageId)
        {
            var message = await _messageRepository.GetMessageByIdWithoutFilter(messageId);
            if (message == null)
                return false;

            var result = await _db.Ado.UseTranAsync(async () =>
            {
                if (!await _messageRepository.RecallMessage(messageId))
                    throw new Exception("撤回消息失败");

                List<ChatEntity> eventChats;
                if (message.Chat_Type == ChatType.Private)
                {
                    eventChats = new List<ChatEntity>();

                    var senderChat = await _chatRepository.GetChatByUserIdAndType(
                        message.Sender_Id, message.Target_Id, ChatType.Private);
                    var receiverChat = await _chatRepository.GetChatByUserIdAndType(
                        message.Target_Id, message.Sender_Id, ChatType.Private);

                    if (senderChat != null) eventChats.Add(senderChat);
                    if (receiverChat != null) eventChats.Add(receiverChat);
                }
                else
                {
                    var members = await _groupMemberRepository.GetGroupMembersByGroupId(message.Target_Id);
                    eventChats = await _chatRepository.GetChatsByUserIdsAndType(
                        members.Select(m => m.User_Id).Distinct().ToList(),
                        message.Target_Id,
                        ChatType.Group);
                }

                var syncEvents = new List<SyncEventEntity>();
                foreach (var chat in eventChats)
                {
                    syncEvents.Add(SyncEventFactory.Create(
                        chat.User_Id,
                        SyncEventTypes.MessageRecalled,
                        messageId,
                        chat.Id));
                    syncEvents.Add(SyncEventFactory.Create(
                        chat.User_Id,
                        SyncEventTypes.ChatChanged,
                        chat.Id));
                }

                if (!await _syncEventRepository.InsertSyncEventsAsync(syncEvents))
                    throw new Exception("写入消息撤回同步事件失败");
            });

            return result.IsSuccess;
        }
    }
}
