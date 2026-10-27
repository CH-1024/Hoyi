using Azure.Core;
using HY.ApiService.Dtos;
using HY.ApiService.Entities;
using HY.ApiService.Enums;
using HY.ApiService.Models;
using HY.ApiService.Repositories;
using HY.ApiService.Tools;
using Mapster;
using SqlSugar;

namespace HY.ApiService.Services
{
    public record ChatsReturn(bool IsSucc, string? Error = null, List<ChatDto>? Chats = null);

    public interface IChatService
    {
        Task<ChatsReturn> GetAllChatsByUserId(long userId);

        Task<bool> ClearChatUnread(long chat_Id);
        Task<bool> ClearChatUnread(long userId, long targetId, ChatType chatType);

        Task<bool> IsUserOwnerChat(long userId, long chatId);
    }


    public class ChatService : IChatService
    {
        private readonly ISqlSugarClient _db;

        private readonly IChatRepository _chatRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupMemberRepository _groupMemberRepository;
        private readonly IMessageRepository _messageRepository;



        public ChatService(ISqlSugarClient db, IChatRepository chatRepository, IUserRepository userRepository, IGroupRepository groupRepository, IGroupMemberRepository groupMemberRepository, IMessageRepository messageRepository)
        {
            _db = db;

            _chatRepository = chatRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _groupMemberRepository = groupMemberRepository;
            _messageRepository = messageRepository;
        }



        public async Task<ChatsReturn> GetAllChatsByUserId(long userId)
        {
            var chatEntities = await _chatRepository.GetChatsByUserId(userId);

            chatEntities = chatEntities.Where(c => !c.Is_Deleted).OrderByDescending(c => c.Last_Msg_Time).ToList();

            var userIds = chatEntities.Where(c => c.Type == ChatType.Private).Select(c => c.Target_Id).Distinct().ToList();
            var groupIds = chatEntities.Where(c => c.Type == ChatType.Group).Select(c => c.Target_Id).Distinct().ToList();
            var lastMsgIds = chatEntities.Select(c => c.Last_Msg_Id).Where(i => i > 0).Distinct().ToList();

            var userMap = (await _userRepository.GetUsersByIds(userIds)).ToDictionary(x => x.Id);
            var groupMap = (await _groupRepository.GetGroupsByIds(groupIds)).ToDictionary(x => x.Id);
            var lastMsgMap = (await _messageRepository.GetMessagesByIds(userId, lastMsgIds)).ToDictionary(x => x.Id);

            var chatDtos = new List<ChatDto>();
            foreach (var chatEntity in chatEntities)
            {
                var chatDto = chatEntity.Adapt<ChatDto>();

                if (chatDto.Type == ChatType.Private && userMap.TryGetValue(chatDto.Target_Id, out var user)) // 单聊 
                {
                    chatDto.Target_Name = user.Nickname;
                    chatDto.Target_Avatar = user.Avatar;
                }
                else if (chatDto.Type == ChatType.Group && groupMap.TryGetValue(chatDto.Target_Id, out var group)) // 群聊
                {
                    chatDto.Target_Name = group.Name;
                    chatDto.Target_Avatar = group.Avatar;
                }

                if (chatEntity.Last_Msg_Id == 0)
                {
                    // 没有Last_Msg
                    chatDto.Last_Msg = null;
                }
                else if (lastMsgMap.TryGetValue(chatEntity.Last_Msg_Id, out var lastMsg))
                {
                    // 存在未删除Last_Msg
                    chatDto.Last_Msg = lastMsg?.Adapt<MessageDto>();
                }
                else
                {
                    // 已删除Last_Msg
                    var msg = await _messageRepository.GetMessageByIdWithoutFilter(chatEntity.Last_Msg_Id);
                    msg.Content = null;
                    msg.Extra = null;
                    msg.Message_Status = MessageStatus.Deleted;
                    chatDto.Last_Msg = msg?.Adapt<MessageDto>();
                }

                chatDtos.Add(chatDto);
            }

            return new (true, null, chatDtos);
        }






        public async Task<bool> ClearChatUnread(long chat_Id)
        {
            return await _chatRepository.ClearChatUnread(chat_Id);
        }

        public async Task<bool> ClearChatUnread(long userId, long targetId, ChatType chatType)
        {
            var chatEntity = await _chatRepository.GetChatByUserIdAndType(userId, targetId, chatType);
            if (chatEntity != null)
            {
                return await _chatRepository.ClearChatUnread(chatEntity.Id);
            }
            return false;
        }



        public async Task<bool> IsUserOwnerChat(long userId, long chatId)
        {
            var chatEntity = await _chatRepository.GetChatByChatId(chatId);

            if (chatEntity == null) return false;

            return chatEntity.User_Id == userId;
        }
    }
}
