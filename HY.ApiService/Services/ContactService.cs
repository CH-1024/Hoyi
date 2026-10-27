using HY.ApiService.Dtos;
using HY.ApiService.Entities;
using HY.ApiService.Enums;
using HY.ApiService.Extensions;
using HY.ApiService.Models;
using HY.ApiService.Repositories;
using Mapster;
using SqlSugar;
using System.Net.Sockets;

namespace HY.ApiService.Services
{
    public record ContactReturn(bool IsSucc, string? Error = null, ContactDto? Contact = null);
    public record ContactsReturn(bool IsSucc, string? Error = null, List<ContactDto>? Contacts = null);
    public record ContactRequestsReturn(bool IsSucc, string? Error = null, List<ContactRequestDto>? ContactRequests = null);

    public record RequestContactReturn(bool IsSucc, string? Error = null, ContactRequestDto? ContactRequest = null, ContactDto? SenderContact = null, ContactDto? ReceiverContact = null, ChatDto? SenderChat = null, ChatDto? ReceiverChat = null, MessageDto? SenderMessage = null, MessageDto? ReceiverMessage = null);
    public record RespondContactReturn(bool IsSucc, string? Error = null, ContactRequestDto? ContactRequest = null, ContactDto? SenderContact = null, ContactDto? ReceiverContact = null, ChatDto? SenderChat = null, ChatDto? ReceiverChat = null, MessageDto? SenderMessage = null, MessageDto? ReceiverMessage = null);

    public interface IContactService
    {
        Task<ContactRequestsReturn> GetAllContactRequestsByUserId(long userId);

        Task<ContactsReturn> GetAllContactsByUserId(long userId);
        Task<ContactReturn> GetContactByUserId(long currentUserId, long targetId);
        Task<ContactReturn> GetContactByHYidOrPhone(long currentUserId, string identity);

        Task<RequestContactReturn> RequestContact(long userId, long contactId, int source, string? message);
        Task<RespondContactReturn> RespondContact(long userId, long contactRequestId, RespondContactHandle handle);
        
        Task<ContactReturn> DeleteContact(long userId, long targetId);
    }


    public class ContactService : IContactService
    {
        private readonly ISqlSugarClient _db;

        private readonly IChatRepository _chatRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;
        private readonly IContactRepository _contactRepository;
        private readonly IContactRequestRepository _contactRequestRepository;
        private readonly IRedisConnectionService _redisConnectionService;


        public ContactService(ISqlSugarClient db, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, IContactRepository contactRepository, IContactRequestRepository contactRequestRepository, IRedisConnectionService redisConnectionService)
        {
            _db = db;

            _chatRepository = chatRepository;
            _messageRepository = messageRepository;
            _userRepository = userRepository;
            _contactRepository = contactRepository;
            _contactRequestRepository = contactRequestRepository;
            _redisConnectionService = redisConnectionService;
        }


        public async Task<ContactRequestsReturn> GetAllContactRequestsByUserId(long currentUserId)
        {
            var contactRequestEntities = await _contactRequestRepository.GetContactRequestsByUserId(currentUserId);

            var contactRequestDtos = contactRequestEntities.Adapt<List<ContactRequestDto>>();

            var senderIds = contactRequestEntities.FindAll(cr => cr.Receiver_Id == currentUserId).Select(cr => cr.Sender_Id).Distinct().ToList();
            var receiverIds = contactRequestEntities.FindAll(cr => cr.Sender_Id == currentUserId).Select(cr => cr.Receiver_Id).Distinct().ToList();

            var userIds = senderIds.Union(receiverIds).ToList();

            var userMap = (await _userRepository.GetUsersByIds(userIds)).ToDictionary(u => u.Id);

            foreach (var contactRequestDto in contactRequestDtos)
            {
                if (userMap.TryGetValue(contactRequestDto.Sender_Id, out var sender))
                {
                    contactRequestDto.Sender_Nickname = sender.Nickname;
                    contactRequestDto.Sender_Avatar = sender.Avatar;
                }
                if (userMap.TryGetValue(contactRequestDto.Receiver_Id, out var receiver))
                {
                    contactRequestDto.Receiver_Nickname = receiver.Nickname;
                    contactRequestDto.Receiver_Avatar = receiver.Avatar;
                }
            }

            return new(true, ContactRequests: contactRequestDtos);
        }

        public async Task<ContactsReturn> GetAllContactsByUserId(long currentUserId)
        {
            var contactEntities = await _contactRepository.GetContactsByUserId(currentUserId);

            contactEntities = contactEntities.Where(c => c.Relation_Status == RelationStatus.Friend).ToList();

            var contactDtos = contactEntities.Adapt<List<ContactDto>>();

            var contactIds = contactEntities.Select(c => c.Contact_Id).Distinct().ToList();
            var contactRequestIds = contactEntities.Select(c => c.Contact_Request_Id).ToList();

            var contactMap = (await _userRepository.GetUsersByIds(contactIds)).ToDictionary(x => x.Id);
            var contactRequestMap = (await _contactRequestRepository.GetContactRequestsByIds(contactRequestIds)).ToDictionary(x => x.Id);

            foreach (var contactDto in contactDtos)
            {
                if (contactMap.TryGetValue(contactDto.Contact_Id, out var contact))
                {
                    contactDto.Contact_HYid = contact.HYid;
                    contactDto.Nickname = contact.Nickname;
                    contactDto.Avatar = contact.Avatar;
                    contactDto.Region = contact.Region;
                    contactDto.Is_Online = await _redisConnectionService.IsUserOnlineAsync(contact.Id);
                }

                if (contactRequestMap.TryGetValue(contactDto.Contact_Request_Id.Value, out var contactRequest))
                {
                    contactDto.Relation_Request_Status = contactRequest.Relation_Request_Status;
                }
            }

            return new(true, Contacts: contactDtos);
        }

        public async Task<ContactReturn> GetContactByUserId(long currentUserId, long targetId)
        {
            var user = await _userRepository.GetUserById(currentUserId);
            if (user == null) return new(false, "当前用户不存在");

            var target = await _userRepository.GetUserById(targetId);
            if (target == null) return new (false, "用户不存在");

            ContactDto? contactDto = null;

            var contactEntity = await _contactRepository.GetUserContactByUserId(currentUserId, targetId);
            if (contactEntity == null)
            {
                contactDto = CreatNoneRelationContact(target.Id, target.Nickname, target.Avatar, target.Region);
                return new (true, null, contactDto);
            }
            else
            {
                contactDto = contactEntity.Adapt<ContactDto>();
                //contactDto.Contact_HYid = target.HYid;    // 通过 targetId 查到的联系人不显示其HYid
                contactDto.Nickname = target.Nickname;
                contactDto.Avatar = target.Avatar;
                contactDto.Region = target.Region;
                contactDto.Contact_Status = target.Status;
                //contactDto.Is_Online = await _redisConnectionService.IsUserOnlineAsync(target.Id);    // 通过 targetId 查到的联系人不显示其Online状态

                var contactRequestEntity = await _contactRequestRepository.GetContactRequestById(contactEntity.Contact_Request_Id);
                contactDto.Relation_Request_Status = contactRequestEntity.Relation_Request_Status;

                return new (true, null, contactDto);
            }
        }

        public async Task<ContactReturn> GetContactByHYidOrPhone(long currentUserId, string identity)
        {
            var user = await _userRepository.GetUserById(currentUserId);
            if (user == null) return new(false, "当前用户不存在");

            UserEntity? target = null;

            if (identity.IsPhone())
            {
                target = await _userRepository.GetUserByPhone(identity);
            }
            else if (identity.IsHYid())
            {
                target = await _userRepository.GetUserByHYid(identity);
            }

            if (target == null) return new (false, "用户不存在");

            ContactDto? contactDto = null;

            var contactEntity = await _contactRepository.GetUserContactByUserId(currentUserId, target.Id);
            if (contactEntity == null)
            {
                contactDto = CreatNoneRelationContact(target.Id, target.Nickname, target.Avatar, target.Region);
                return new (true, null, contactDto);
            }
            else
            {
                contactDto = contactEntity.Adapt<ContactDto>();
                //contactDto.Contact_HYid = target.HYid;    // 通过 identity 查到的联系人不显示其HYid
                contactDto.Nickname = target.Nickname;
                contactDto.Avatar = target.Avatar;
                contactDto.Region = target.Region;
                contactDto.Contact_Status = target.Status;
                //contactDto.Is_Online = await _redisConnectionService.IsUserOnlineAsync(target.Id);    // 通过 identity 查到的联系人不显示其Online状态

                var contactRequestEntity = await _contactRequestRepository.GetContactRequestById(contactEntity.Contact_Request_Id);
                contactDto.Relation_Request_Status = contactRequestEntity.Relation_Request_Status;

                return new (true, null, contactDto);
            }
        }


        public async Task<RequestContactReturn> RequestContact(long senderId, long contactId, int source, string? message)
        {
            if (senderId == contactId)
            {
                return new(false, "不能给自己发送好友请求");
            }

            var sender = await _userRepository.GetUserById(senderId);
            if (sender == null) return new(false, "当前用户不存在");

            var receiver = await _userRepository.GetUserById(contactId);
            if (receiver == null) return new(false, "用户不存在");


            var senderContact = await _contactRepository.GetUserContactByUserId(senderId, receiver.Id);
            var receiverContact = await _contactRepository.GetUserContactByUserId(receiver.Id, senderId);

            if (senderContact?.Relation_Status == RelationStatus.Friend && receiverContact?.Relation_Status == RelationStatus.Friend)
            {
                return new(false, "你们已经是好友了");
            }
            else if (receiverContact?.Relation_Status == RelationStatus.Blocked)
            {
                return new(false, "对方已经将你加入黑名单");

            }

            var senderContactRequest = await _contactRequestRepository.GetPendingContactRequestByUserId(senderId, receiver.Id);
            if(senderContactRequest != null)
            {
                return new(false, "你已经发送了好友请求");
            }

            var receiverContactRequest = await _contactRequestRepository.GetPendingContactRequestByUserId(receiver.Id, senderId);
            if (receiverContactRequest != null)
            {
                // (避免交叉请求)
                return new(false, "对方已经发送好友请求");
            }


            ContactRequestEntity? contactRequestEntity = null;
            ContactDto? senderContactDto = null;
            ContactDto? receiverContactDto = null;
            ChatDto? senderChatDto = null;
            ChatDto? receiverChatDto = null;
            MessageDto? senderSysMsgDto = null;
            MessageDto? receiverSysMsgDto = null;

            // 开启事务
            var result = await _db.Ado.UseTranAsync(async () =>
            {
                // 创建联系人请求
                contactRequestEntity = new ContactRequestEntity
                {
                    Sender_Id = senderId,
                    Receiver_Id = receiver.Id,
                    Message = message,
                    Source = source,
                    Relation_Request_Status = RelationRequestStatus.Pending,
                    Created_At = DateTime.UtcNow
                };
                var id = await _contactRequestRepository.CreateContactRequest(contactRequestEntity);
                if (id <= 0) throw new Exception("创建好友请求记录失败");

                if (receiverContact?.Relation_Status == RelationStatus.Friend)
                {
                    // 已经是对方好友了

                    contactRequestEntity.Relation_Request_Status = RelationRequestStatus.Accepted;
                    contactRequestEntity.Handled_At = DateTime.UtcNow;
                    var bol = await _contactRequestRepository.UpdateContactRequest(contactRequestEntity);
                    if (!bol) throw new Exception("更新好友请求状态为已同意失败");

                    #region SenderContact
                    senderContact.Contact_Request_Id = contactRequestEntity.Id;
                    senderContact.Relation_Status = RelationStatus.Friend;
                    var bol1 = await _contactRepository.UpdateContact(senderContact);
                    if (!bol1) throw new Exception("更新发送方联系人记录失败");

                    senderContactDto = senderContact.Adapt<ContactDto>();
                    senderContactDto.Contact_HYid = receiver.HYid;
                    senderContactDto.Nickname = receiver.Nickname;
                    senderContactDto.Avatar = receiver.Avatar;
                    senderContactDto.Region = receiver.Region;
                    senderContactDto.Contact_Status = receiver.Status;
                    senderContactDto.Is_Online = await _redisConnectionService.IsUserOnlineAsync(receiver.Id);
                    senderContactDto.Relation_Request_Status = contactRequestEntity.Relation_Request_Status;
                    #endregion

                    #region ReceiverContact
                    receiverContact.Contact_Request_Id = contactRequestEntity.Id;
                    receiverContact.Relation_Status = RelationStatus.Friend;
                    var bol2 = await _contactRepository.UpdateContact(receiverContact);
                    if (!bol2) throw new Exception("更新接收方联系人记录失败");

                    receiverContactDto = receiverContact.Adapt<ContactDto>();
                    receiverContactDto.Contact_HYid = sender.HYid;
                    receiverContactDto.Nickname = sender.Nickname;
                    receiverContactDto.Avatar = sender.Avatar;
                    receiverContactDto.Region = sender.Region;
                    receiverContactDto.Contact_Status = sender.Status;
                    receiverContactDto.Is_Online = await _redisConnectionService.IsUserOnlineAsync(sender.Id);
                    receiverContactDto.Relation_Request_Status = contactRequestEntity.Relation_Request_Status;
                    #endregion

                    #region SenderChat
                    var senderSysMsg = new MessageEntity
                    {
                        Chat_Type = ChatType.Private,
                        Sender_Id = contactRequestEntity.Sender_Id,
                        Target_Id = contactRequestEntity.Receiver_Id,
                        Message_Type = MessageType.System,
                        Content = $"你们已经是好友，可以开始聊天了！",
                        Message_Status = MessageStatus.Sented,
                        Created_At = DateTime.UtcNow
                    };
                    var sMsgId = await _messageRepository.InsertMessage(senderSysMsg);
                    if (sMsgId <= 0) throw new Exception("创建发送方最后一条消息记录失败");

                    senderSysMsgDto = senderSysMsg.Adapt<MessageDto>();

                    var senderChatEntity = await _chatRepository.GetChatByUserIdAndType(contactRequestEntity.Sender_Id, contactRequestEntity.Receiver_Id, ChatType.Private);
                    senderChatEntity.Last_Msg_Id = sMsgId;
                    senderChatEntity.Read_Msg_Id = 0;
                    senderChatEntity.Unread_Count = 1;
                    senderChatEntity.Is_Top = false;
                    senderChatEntity.Is_Deleted = false;
                    senderChatEntity.Last_Msg_Time = senderSysMsg.Created_At;
                    var bol3 = await _chatRepository.UpdateChat(senderChatEntity);
                    if (!bol3) throw new Exception("更新发送方聊天记录失败");

                    senderChatDto = senderChatEntity.Adapt<ChatDto>();
                    senderChatDto.Target_Name = receiver.Nickname;
                    senderChatDto.Target_Avatar = receiver.Avatar;
                    senderChatDto.Last_Msg = senderSysMsgDto;

                    #endregion

                    #region ReceiverChat
                    var receiverSysMsg = new MessageEntity
                    {
                        Chat_Type = ChatType.Private,
                        Sender_Id = contactRequestEntity.Receiver_Id,
                        Target_Id = contactRequestEntity.Sender_Id,
                        Message_Type = MessageType.System,
                        Content = $"你们已经是好友，可以开始聊天了！",
                        Message_Status = MessageStatus.Sented,
                        Created_At = DateTime.UtcNow
                    };
                    var rMsgId = await _messageRepository.InsertMessage(receiverSysMsg);
                    if (rMsgId <= 0) throw new Exception("创建接收方最后一条消息记录失败");

                    receiverSysMsgDto = receiverSysMsg.Adapt<MessageDto>();

                    var receiverChatEntity = await _chatRepository.GetChatByUserIdAndType(contactRequestEntity.Receiver_Id, contactRequestEntity.Sender_Id, ChatType.Private);
                    receiverChatEntity.Last_Msg_Id = rMsgId;
                    receiverChatEntity.Read_Msg_Id = 0;
                    receiverChatEntity.Unread_Count += 1;
                    receiverChatEntity.Is_Top = false;
                    receiverChatEntity.Is_Deleted = false;
                    receiverChatEntity.Last_Msg_Time = receiverSysMsg.Created_At;
                    var bol4 = await _chatRepository.UpdateChat(receiverChatEntity);
                    if (!bol4) throw new Exception("更新接收方聊天记录失败");

                    receiverChatDto = receiverChatEntity.Adapt<ChatDto>();
                    receiverChatDto.Target_Name = sender.Nickname;
                    receiverChatDto.Target_Avatar = sender.Avatar;
                    receiverChatDto.Last_Msg = receiverSysMsgDto;
                    #endregion
                }
            });

            // ---------- 事务结束 ----------
            if (!result.IsSuccess) return new(false, result.ErrorMessage);

            var contactRequestDto = contactRequestEntity.Adapt<ContactRequestDto>();
            contactRequestDto.Sender_Avatar = sender.Avatar;
            contactRequestDto.Sender_Nickname = sender.Nickname;
            contactRequestDto.Receiver_Avatar = receiver.Avatar;
            contactRequestDto.Receiver_Nickname = receiver.Nickname;

            return new(true, null, contactRequestDto, senderContactDto, receiverContactDto, senderChatDto, receiverChatDto, senderSysMsgDto, receiverSysMsgDto);
        }

        public async Task<RespondContactReturn> RespondContact(long userId, long contactRequestId, RespondContactHandle handle)
        {
            var contactRequestEntity = await _contactRequestRepository.GetContactRequestById(contactRequestId);
            if (contactRequestEntity?.Relation_Request_Status != RelationRequestStatus.Pending)
            {
                return new(false, "好友请求已处理");
            }

            var userEntities = await _userRepository.GetUsersByIds([contactRequestEntity.Sender_Id, contactRequestEntity.Receiver_Id]);
            if (userEntities.Count != 2)
            {
                return new(false, "获取用户信息失败");
            }

            var sender = userEntities.First(u => u.Id == contactRequestEntity.Sender_Id);
            var receiver = userEntities.First(u => u.Id == contactRequestEntity.Receiver_Id);

            // 撤销好友请求
            if (handle == RespondContactHandle.Revoked && contactRequestEntity.Sender_Id == userId && contactRequestEntity.Relation_Request_Status == RelationRequestStatus.Pending)
            {
                // 更新好友请求状态为已撤销
                contactRequestEntity.Relation_Request_Status = RelationRequestStatus.Revoked;
                contactRequestEntity.Handled_At = DateTime.UtcNow;

                var bol = await _contactRequestRepository.UpdateContactRequest(contactRequestEntity);
                if (!bol) return new(false, "更新好友请求状态失败");

                var contactRequestDto = contactRequestEntity.Adapt<ContactRequestDto>();
                contactRequestDto.Sender_Nickname = sender.Nickname;
                contactRequestDto.Sender_Avatar = sender.Avatar;
                contactRequestDto.Receiver_Nickname = receiver.Nickname;
                contactRequestDto.Receiver_Avatar = receiver.Avatar;

                return new(true, null, contactRequestDto, null, null, null, null, null, null);
            }
            // 拒绝好友请求
            else if (handle == RespondContactHandle.Declined && contactRequestEntity.Receiver_Id == userId && contactRequestEntity.Relation_Request_Status == RelationRequestStatus.Pending)
            {
                // 更新好友请求状态为已拒绝
                contactRequestEntity.Relation_Request_Status = RelationRequestStatus.Declined;
                contactRequestEntity.Handled_At = DateTime.UtcNow;

                var bol = await _contactRequestRepository.UpdateContactRequest(contactRequestEntity);
                if (!bol) return new(false, "更新好友请求状态失败");

                var contactRequestDto = contactRequestEntity.Adapt<ContactRequestDto>();
                contactRequestDto.Sender_Nickname = sender.Nickname;
                contactRequestDto.Sender_Avatar = sender.Avatar;
                contactRequestDto.Receiver_Nickname = receiver.Nickname;
                contactRequestDto.Receiver_Avatar = receiver.Avatar;

                return new(true, null, contactRequestDto, null, null, null, null, null, null);
            }
            // 同意好友请求
            else if (handle == RespondContactHandle.Accepted && contactRequestEntity.Receiver_Id == userId && contactRequestEntity.Relation_Request_Status == RelationRequestStatus.Pending)
            {
                ContactDto? senderContactDto = null;
                ContactDto? receiverContactDto = null;
                ChatDto? senderChatDto = null;
                ChatDto? receiverChatDto = null;
                MessageDto? senderSysMsgDto = null;
                MessageDto? receiverSysMsgDto = null;

                // 开启事务
                var result = await _db.Ado.UseTranAsync(async () =>
                {
                    // 更新好友请求状态为已同意
                    contactRequestEntity.Relation_Request_Status = RelationRequestStatus.Accepted;
                    contactRequestEntity.Handled_At = DateTime.UtcNow;
                    if (!await _contactRequestRepository.UpdateContactRequest(contactRequestEntity))
                    {
                        throw new Exception("更新好友请求状态为已同意失败");
                    }

                    #region SenderContact
                    var senderContact = await _contactRepository.GetUserContactByUserId(contactRequestEntity.Sender_Id, contactRequestEntity.Receiver_Id);
                    if (senderContact == null)
                    {
                        senderContact = new ContactEntity
                        {
                            User_Id = contactRequestEntity.Sender_Id,
                            Contact_Id = contactRequestEntity.Receiver_Id,
                            Contact_Request_Id = contactRequestEntity.Id,
                            Relation_Status = RelationStatus.Friend,
                            Created_At = DateTime.UtcNow
                        };
                        var count = await _contactRepository.CreateContact(senderContact);
                        if (count <= 0) throw new Exception("创建发送方联系人记录失败");
                    }
                    else
                    {
                        senderContact.Contact_Request_Id = contactRequestEntity.Id;
                        senderContact.Relation_Status = RelationStatus.Friend;
                        var bol1 = await _contactRepository.UpdateContact(senderContact);
                        if (!bol1) throw new Exception("更新发送方联系人记录失败");
                    }

                    senderContactDto = senderContact.Adapt<ContactDto>();
                    senderContactDto.Contact_HYid = receiver.HYid;
                    senderContactDto.Nickname = receiver.Nickname;
                    senderContactDto.Avatar = receiver.Avatar;
                    senderContactDto.Region = receiver.Region;
                    senderContactDto.Contact_Status = receiver.Status;
                    senderContactDto.Is_Online = await _redisConnectionService.IsUserOnlineAsync(receiver.Id);
                    senderContactDto.Relation_Request_Status = contactRequestEntity.Relation_Request_Status;
                    #endregion

                    #region ReceiverContact
                    var receiverContact = await _contactRepository.GetUserContactByUserId(contactRequestEntity.Receiver_Id, contactRequestEntity.Sender_Id);
                    if (receiverContact == null)
                    {
                        receiverContact = new ContactEntity
                        {
                            User_Id = contactRequestEntity.Receiver_Id,
                            Contact_Id = contactRequestEntity.Sender_Id,
                            Contact_Request_Id = contactRequestEntity.Id,
                            Relation_Status = RelationStatus.Friend,
                            Created_At = DateTime.UtcNow
                        };
                        var count = await _contactRepository.CreateContact(receiverContact);
                        if (count <= 0) throw new Exception("创建接收方联系人记录失败");
                    }
                    else
                    {
                        receiverContact.Contact_Request_Id = contactRequestEntity.Id;
                        receiverContact.Relation_Status = RelationStatus.Friend;
                        var bol1 = await _contactRepository.UpdateContact(receiverContact);
                        if (!bol1) throw new Exception("更新接收方联系人记录失败");
                    }

                    receiverContactDto = receiverContact.Adapt<ContactDto>();
                    receiverContactDto.Contact_HYid = sender.HYid;
                    receiverContactDto.Nickname = sender.Nickname;
                    receiverContactDto.Avatar = sender.Avatar;
                    receiverContactDto.Region = sender.Region;
                    receiverContactDto.Contact_Status = sender.Status;
                    receiverContactDto.Is_Online = await _redisConnectionService.IsUserOnlineAsync(sender.Id);
                    receiverContactDto.Relation_Request_Status = contactRequestEntity.Relation_Request_Status;
                    #endregion


                    #region SenderChat

                    var senderSysMsg = new MessageEntity
                    {
                        Chat_Type = ChatType.Private,
                        Sender_Id = contactRequestEntity.Sender_Id,
                        Target_Id = contactRequestEntity.Receiver_Id,
                        Message_Type = MessageType.System,
                        Content = $"你们已经是好友，可以开始聊天了！",
                        Message_Status = MessageStatus.Sented,
                        Created_At = DateTime.UtcNow
                    };
                    var sMsgId = await _messageRepository.InsertMessage(senderSysMsg);
                    if (sMsgId <= 0) throw new Exception("创建发送方最后一条消息记录失败");

                    senderSysMsgDto = senderSysMsg.Adapt<MessageDto>();

                    var senderChatEntity = await _chatRepository.GetChatByUserIdAndType(contactRequestEntity.Sender_Id, contactRequestEntity.Receiver_Id, ChatType.Private);
                    if (senderChatEntity == null)
                    {
                        senderChatEntity = new ChatEntity
                        {
                            Type = ChatType.Private,
                            User_Id = contactRequestEntity.Sender_Id,
                            Target_Id = contactRequestEntity.Receiver_Id,
                            Last_Msg_Id = sMsgId,
                            Read_Msg_Id = 0,
                            Unread_Count = 1,
                            Is_Top = false,
                            Is_Deleted = false,
                            Last_Msg_Time = senderSysMsg.Created_At
                        };
                        var id = await _chatRepository.CreateChat(senderChatEntity);
                        if (id <= 0) throw new Exception("创建发送方聊天记录失败");
                    }
                    else
                    {
                        senderChatEntity.Last_Msg_Id = sMsgId;
                        senderChatEntity.Read_Msg_Id = 0;
                        senderChatEntity.Unread_Count = 1;
                        senderChatEntity.Is_Top = false;
                        senderChatEntity.Is_Deleted = false;
                        senderChatEntity.Last_Msg_Time = senderSysMsg.Created_At;
                        var bol = await _chatRepository.UpdateChat(senderChatEntity);
                        if (!bol) throw new Exception("更新发送方聊天记录失败");
                    }

                    senderChatDto = senderChatEntity.Adapt<ChatDto>();
                    senderChatDto.Target_Name = receiver.Nickname;
                    senderChatDto.Target_Avatar = receiver.Avatar;
                    senderChatDto.Last_Msg = senderSysMsgDto;

                    #endregion

                    #region ReceiverChat

                    var receiverSysMsg = new MessageEntity
                    {
                        Chat_Type = ChatType.Private,
                        Sender_Id = contactRequestEntity.Receiver_Id,
                        Target_Id = contactRequestEntity.Sender_Id,
                        Message_Type = MessageType.System,
                        Content = $"你们已经是好友，可以开始聊天了！",
                        Message_Status = MessageStatus.Sented,
                        Created_At = DateTime.UtcNow
                    };
                    var rMsgId = await _messageRepository.InsertMessage(receiverSysMsg);
                    if (rMsgId <= 0) throw new Exception("创建接收方最后一条消息记录失败");

                    receiverSysMsgDto = receiverSysMsg.Adapt<MessageDto>();

                    var receiverChatEntity = await _chatRepository.GetChatByUserIdAndType(contactRequestEntity.Receiver_Id, contactRequestEntity.Sender_Id, ChatType.Private);
                    if (receiverChatEntity == null)
                    {
                        receiverChatEntity = new ChatEntity
                        {
                            Type = ChatType.Private,
                            User_Id = contactRequestEntity.Receiver_Id,
                            Target_Id = contactRequestEntity.Sender_Id,
                            Last_Msg_Id = rMsgId,
                            Read_Msg_Id = 0,
                            Unread_Count = 1,
                            Is_Top = false,
                            Is_Deleted = false,
                            Last_Msg_Time = receiverSysMsg.Created_At
                        };
                        var id = await _chatRepository.CreateChat(receiverChatEntity);
                        if (id <= 0) throw new Exception("创建接收方聊天记录失败");
                    }
                    else
                    {
                        receiverChatEntity.Last_Msg_Id = rMsgId;
                        receiverChatEntity.Read_Msg_Id = 0;
                        receiverChatEntity.Unread_Count = 1;
                        receiverChatEntity.Is_Top = false;
                        receiverChatEntity.Is_Deleted = false;
                        receiverChatEntity.Last_Msg_Time = receiverSysMsg.Created_At;
                        var bol = await _chatRepository.UpdateChat(receiverChatEntity);
                        if (!bol) throw new Exception("更新接收方聊天记录失败");
                    }

                    receiverChatDto = receiverChatEntity.Adapt<ChatDto>();
                    receiverChatDto.Target_Name = sender.Nickname;
                    receiverChatDto.Target_Avatar = sender.Avatar;
                    receiverChatDto.Last_Msg = receiverSysMsgDto;

                    #endregion

                });

                // ---------- 事务结束 ----------
                if (!result.IsSuccess) return new(false, result.ErrorMessage);

                var contactRequestDto = contactRequestEntity.Adapt<ContactRequestDto>();
                contactRequestDto.Sender_Nickname = sender.Nickname;
                contactRequestDto.Sender_Avatar = sender.Avatar;
                contactRequestDto.Receiver_Nickname = receiver.Nickname;
                contactRequestDto.Receiver_Avatar = receiver.Avatar;

                return new(true, null, contactRequestDto, senderContactDto, receiverContactDto, senderChatDto, receiverChatDto, senderSysMsgDto, receiverSysMsgDto);
            }
            else
            {
                return new(false, "获取好友请求信息失败");
            }
        }


        public async Task<ContactReturn> DeleteContact(long userId, long targetId)
        {
            var contactEntity = await _contactRepository.GetUserContactByUserId(userId, targetId);
            if (contactEntity == null)
            {
                return new (false, "联系人关系不存在");
            }

            // 开启事务
            var result = await _db.Ado.UseTranAsync(async () =>
            {
                contactEntity.Relation_Status = RelationStatus.Deleted;
                var bol1 = await _contactRepository.UpdateContact(contactEntity);
                if (!bol1)
                {
                    throw new Exception("删除联系人关系失败");
                }

                var chatEntity = await _chatRepository.GetChatByUserIdAndType(userId, targetId, ChatType.Private);
                chatEntity.Is_Deleted = true;
                var bol2 = await _chatRepository.UpdateChat(chatEntity);
                if (!bol2)
                {
                    throw new Exception("删除聊天记录失败");
                }
            });

            // ---------- 事务结束 ----------
            if (!result.IsSuccess)
            {
                return new (false, result.ErrorMessage);
            }

            return new (true);
        }





        ContactDto CreatNoneRelationContact(long userId, string? nickname, string? avatar, string? region)
        {
            return new ContactDto
            {
                Contact_Id = userId,
                Contact_HYid = null,
                Nickname = nickname,
                Avatar = avatar,
                Region = region,
                Remark = null,
                Contact_Status = null,
                Relation_Request_Status = RelationRequestStatus.None,
                Relation_Status = RelationStatus.None,
                Created_At = null,
                Is_Online = false
            };
        }

    }
}
