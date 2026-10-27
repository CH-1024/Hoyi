using HY.ApiService.Controllers.Requests;
using HY.ApiService.Dtos;
using HY.ApiService.Enums;
using HY.ApiService.Hubs;
using HY.ApiService.Hubs.Requests;
using HY.ApiService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HY.ApiService.Services
{
    public interface IChatNotificationService
    {
        Task UserOnlineNotify(long currentUserId);
        Task UserOfflineNotify(long currentUserId);

        Task ReadAllNotify(long userId, long chatId, int platform);

        Task SendMessageNotify(MessageDto messageDto, int platform);
        Task RecallMessageNotify(MessageDto messageDto, int platform);
        Task DeleteMessageNotify(long currentUserId, MessageDto messageDto, int platform);

        Task CreateCallNotify(CallInfo callDto);
        Task CancelCallNotify(CallInfo callDto);
        Task AcceptCallNotify(CallInfo callDto);
        Task RejectCallNotify(CallInfo callDto);
        Task AbnormalCallNotify(CallInfo callDto, long currentUserId);
        Task HangUpCallNotify(CallInfo callDto, long currentUserId);
        Task ExpiryCallNotify(CallInfo callDto);
        Task SendCallMessageNotify(MessageDto messageDto);

        Task RequestContactNotify(RequestContactReturn result, int platform);
        Task RespondContactNotify(RespondContactHandle handle, RespondContactReturn result, int platform);
        Task DeleteContactNotify(long currentUserId, long contactId, int platform);

        Task UpdateHeadNotify(long userId, string url, int platform);
        Task UpdateProfileNotify(long userId, UpdateUserRequest updateUserRequest, int platform);
    }


    public class ChatNotificationService : IChatNotificationService
    {
        readonly IHubContext<ChatHub> _chatHub;
        readonly IRedisConnectionService _redisConnectionService;

        readonly IChatService _chatService;
        readonly IUserService _userService;
        readonly IMessageService _messageService;
        readonly IContactService _contactService;
        readonly IGroupMemberService _groupMemberService;


        public ChatNotificationService(IHubContext<ChatHub> chatHub, IRedisConnectionService redisConnectionService, IChatService chatService, IUserService userService, IMessageService messageService, IContactService contactService, IGroupMemberService groupMemberService)
        {
            _chatHub = chatHub;
            _redisConnectionService = redisConnectionService;

            _chatService = chatService;
            _userService = userService;
            _messageService = messageService;
            _contactService = contactService;
            _groupMemberService = groupMemberService;
        }



        public async Task UserOnlineNotify(long currentUserId)
        {
            var result = await _contactService.GetAllContactsByUserId(currentUserId);
            if (!result.IsSucc)
            {
                return;
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            foreach (var contactId in result.Contacts.Select(c => c.Contact_Id).ToList())
            {
                var contactConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(contactId);
                await Parallel.ForEachAsync(contactConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
                {
                    try
                    {
                        await _chatHub.Clients.Client(connectionId).SendAsync("UserOnline", currentUserId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // 记录日志
                        // _logger.LogError(ex, "通知失败，ConnectionId: {ConnectionId}", connectionId);
                    }
                });
            }
        }

        public async Task UserOfflineNotify(long currentUserId)
        {
            var result = await _contactService.GetAllContactsByUserId(currentUserId);
            if (!result.IsSucc)
            {
                return;
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            foreach (var contactId in result.Contacts.Select(c => c.Contact_Id).ToList())
            {
                var contactConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(contactId);
                await Parallel.ForEachAsync(contactConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
                {
                    try
                    {
                        await _chatHub.Clients.Client(connectionId).SendAsync("UserOffline", currentUserId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // 记录日志
                        // _logger.LogError(ex, "通知失败，ConnectionId: {ConnectionId}", connectionId);
                    }
                });
            }
        }


        public async Task ReadAllNotify(long currentUserId, long chatId, int platform)
        {
            #region 通知自己其他在线设备
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            var otherPlatformConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(currentUserId, platform);
            await Parallel.ForEachAsync(otherPlatformConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("ReadAll", chatId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "全部已读失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }


        public async Task SendMessageNotify(MessageDto messageDto, int platform)
        {
            if (messageDto == null)
            {
                return;
            }

            if (messageDto.Chat_Type == ChatType.Private)
            {
                // 单人

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 20,
                    CancellationToken = CancellationToken.None
                };


                #region 通知对方所有在线设备
                var hasSuccess = 0;
                var receiverConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(messageDto.Target_Id);
                await Parallel.ForEachAsync(receiverConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
                {
                    try
                    {
                        var success = await _chatHub.Clients.Client(connectionId).InvokeAsync<bool>("ReceiveMessage", messageDto, cancellationToken);
                        if (success) Interlocked.Exchange(ref hasSuccess, 1);
                    }
                    catch (Exception ex)
                    {
                        // 记录日志
                        // _logger.LogError(ex, "发送消息失败，ConnectionId: {ConnectionId}", connectionId);
                    }
                });

                // 接收成功，更新未读数
                if (hasSuccess == 1)
                {
                    await _chatService.ClearChatUnread(messageDto.Target_Id, messageDto.Sender_Id, ChatType.Private);
                }
                #endregion


                #region 通知自己其他在线设备
                var otherPlatformConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(messageDto.Sender_Id, platform);
                await Parallel.ForEachAsync(otherPlatformConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
                {
                    try
                    {
                        await _chatHub.Clients.Client(connectionId).InvokeAsync<bool>("ReceiveMessage", messageDto, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // 记录日志
                        // _logger.LogError(ex, "发送消息失败，ConnectionId: {ConnectionId}", connectionId);
                    }
                });

                // 自己发的消息不需要更新未读数
                #endregion
            }
            else if (messageDto.Chat_Type == ChatType.Group)
            {
                // 群聊

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 20,
                    CancellationToken = CancellationToken.None
                };

                // 获取群成员列表
                var groupMembers = await _groupMemberService.GetGroupMembersByGroupId(messageDto.Target_Id);

                var receiverConnections = new List<(long UserId, string ConnectionId)>();
                var otherPlatformConnections = new List<(long UserId, string ConnectionId)>();

                foreach (var member in groupMembers)
                {
                    if (member.User_Id == messageDto.Sender_Id)
                    {
                        // 自己其他在线设备
                        var connectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(member.User_Id, platform);
                        otherPlatformConnections.AddRange(connectionIds.Select(connId => (UserId: member.User_Id, ConnectionId: connId)));
                    }
                    else
                    {
                        // 成员所有在线设备
                        var connectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(member.User_Id);
                        receiverConnections.AddRange(connectionIds.Select(connId => (UserId: member.User_Id, ConnectionId: connId)));
                    }
                }

                #region 通知成员所有在线设备
                foreach (var group in receiverConnections.GroupBy(r => r.UserId))
                {
                    var hasSuccess = 0;

                    await Parallel.ForEachAsync(group.Select(r => r.ConnectionId), parallelOptions, async (connectionId, cancellationToken) =>
                    {
                        try
                        {
                            var success = await _chatHub.Clients.Client(connectionId).InvokeAsync<bool>("ReceiveMessage", messageDto, cancellationToken);
                            if (success) Interlocked.Exchange(ref hasSuccess, 1);
                        }
                        catch (Exception ex)
                        {
                            // 记录日志
                            // _logger.LogError(ex, "发送消息失败，ConnectionId: {ConnectionId}", connectionId);
                        }
                    });

                    // 接收成功，更新未读数
                    if (hasSuccess == 1)
                    {
                        await _chatService.ClearChatUnread(group.Key, messageDto.Target_Id, ChatType.Group);
                    }
                }
                #endregion


                #region 通知自己其他在线设备
                await Parallel.ForEachAsync(otherPlatformConnections.Select(r => r.ConnectionId), parallelOptions, async (connectionId, cancellationToken) =>
                {
                    try
                    {
                        await _chatHub.Clients.Client(connectionId).InvokeAsync<bool>("ReceiveMessage", messageDto, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // 记录日志
                        // _logger.LogError(ex, "发送消息失败，ConnectionId: {ConnectionId}", connectionId);
                    }
                });

                // 自己发的消息不需要更新未读数
                #endregion
            }
        }

        public async Task RecallMessageNotify(MessageDto messageDto, int platform)
        {
            if (messageDto == null)
            {
                return;
            }

            // 通知接收者撤回消息
            if (messageDto.Chat_Type == ChatType.Private)
            {
                // 单人

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 20,
                    CancellationToken = CancellationToken.None
                };


                #region 通知对方所有在线设备
                var receiverConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(messageDto.Target_Id);
                await Parallel.ForEachAsync(receiverConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
                {
                    try
                    {
                        await _chatHub.Clients.Client(connectionId).SendAsync("RecallMessage", messageDto, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // 记录日志
                        // _logger.LogError(ex, "撤回消息失败，ConnectionId: {ConnectionId}", connectionId);
                    }
                });
                #endregion


                #region 通知自己其他在线设备
                var otherPlatformConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(messageDto.Sender_Id, platform);
                await Parallel.ForEachAsync(otherPlatformConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
                {
                    try
                    {
                        await _chatHub.Clients.Client(connectionId).SendAsync("RecallMessage", messageDto, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // 记录日志
                        // _logger.LogError(ex, "撤回消息失败，ConnectionId: {ConnectionId}", connectionId);
                    }
                });
                #endregion
            }
            else if (messageDto.Chat_Type == ChatType.Group)
            {
                // 群聊
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 20,
                    CancellationToken = CancellationToken.None
                };

                // 获取群成员列表
                var groupMembers = await _groupMemberService.GetGroupMembersByGroupId(messageDto.Target_Id);

                var receiverConnections = new List<(long UserId, string ConnectionId)>();
                var otherPlatformConnections = new List<(long UserId, string ConnectionId)>();

                foreach (var member in groupMembers)
                {
                    if (member.User_Id == messageDto.Sender_Id)
                    {
                        // 自己其他在线设备
                        var connectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(member.User_Id, platform);
                        otherPlatformConnections.AddRange(connectionIds.Select(connId => (UserId: member.User_Id, ConnectionId: connId)));
                    }
                    else
                    {
                        // 成员所有在线设备
                        var connectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(member.User_Id);
                        receiverConnections.AddRange(connectionIds.Select(connId => (UserId: member.User_Id, ConnectionId: connId)));
                    }
                }

                #region 通知成员所有在线设备
                foreach (var group in receiverConnections.GroupBy(r => r.UserId))
                {
                    await Parallel.ForEachAsync(group.Select(r => r.ConnectionId), parallelOptions, async (connectionId, cancellationToken) =>
                    {
                        try
                        {
                            await _chatHub.Clients.Client(connectionId).SendAsync("RecallMessage", messageDto, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            // 记录日志
                            // _logger.LogError(ex, "撤回消息失败，ConnectionId: {ConnectionId}", connectionId);
                        }
                    });
                }
                #endregion


                #region 通知自己其他在线设备
                await Parallel.ForEachAsync(otherPlatformConnections.Select(r => r.ConnectionId), parallelOptions, async (connectionId, cancellationToken) =>
                {
                    try
                    {
                        await _chatHub.Clients.Client(connectionId).SendAsync("RecallMessage", messageDto, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // 记录日志
                        // _logger.LogError(ex, "撤回消息失败，ConnectionId: {ConnectionId}", connectionId);
                    }
                });
                #endregion
            }
        }

        public async Task DeleteMessageNotify(long currentUserId, MessageDto messageDto, int platform)
        {
            #region 通知自己其他在线设备
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            var otherPlatformConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(currentUserId, platform);
            await Parallel.ForEachAsync(otherPlatformConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("DeleteMessage", messageDto, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "删除信息失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }


        public async Task CreateCallNotify(CallInfo callDto)
        {
            if (callDto == null) return;

            var callId = callDto.CallId;
            var callType = callDto.CallType;
            var callerId = callDto.CallerId;
            var callerPlatform = callDto.CallerPlatform;
            var calleeId = callDto.CalleeId;
            var expiry = callDto.ExpiryAt;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };
            var rcr = new ReceiveCallRequest()
            {
                CallId = callId,
                CallType = callType,
                CallerId = callerId,
            };

            #region 通知被呼叫方所有在线设备
            var calleeConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(calleeId);
            await Parallel.ForEachAsync(calleeConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("ReceiveCall", rcr, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "[CreateCallNotification] : ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }

        public async Task AcceptCallNotify(CallInfo callDto)
        {
            if (callDto == null) return;

            var callId = callDto.CallId;
            var callType = callDto.CallType;
            var callerId = callDto.CallerId;
            var callerPlatform = callDto.CallerPlatform;
            var calleeId = callDto.CalleeId;
            var calleePlatform = callDto.CalleePlatform;
            var expiry = callDto.ExpiryAt;
            var start = callDto.StartAt!.Value;


            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            #region 通知呼叫方在线设备
            var callerConnectionId = await _redisConnectionService.GetConnectionIdAsync(callerId, callerPlatform);
            if (!string.IsNullOrEmpty(callerConnectionId)) await _chatHub.Clients.Client(callerConnectionId!).SendAsync("CallAccepted", callId, start, parallelOptions.CancellationToken);
            #endregion


            #region 通知被呼叫方在线设备
            var calleeConnectionId = await _redisConnectionService.GetConnectionIdAsync(calleeId, calleePlatform);
            if (!string.IsNullOrEmpty(calleeConnectionId)) await _chatHub.Clients.Client(calleeConnectionId!).SendAsync("CallAccepted", callId, start, parallelOptions.CancellationToken);
            #endregion


            #region 通知被呼叫方其他在线设备
            var calleeConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(calleeId, calleePlatform);
            await Parallel.ForEachAsync(calleeConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("CallHandled", callId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "[AcceptCallNotification] : ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }

        public async Task CancelCallNotify(CallInfo callDto)
        {
            if (callDto == null) return;

            var callId = callDto.CallId;
            var callType = callDto.CallType;
            var callerId = callDto.CallerId;
            var callerPlatform = callDto.CallerPlatform;
            var calleeId = callDto.CalleeId;
            var create = callDto.CreateAt;
            var expiry = callDto.ExpiryAt;


            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            #region 通知被呼叫方所有在线设备
            var calleeConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(calleeId);
            await Parallel.ForEachAsync(calleeConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("CallCanceled", callId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "[CancelCallNotification] : ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }

        public async Task RejectCallNotify(CallInfo callDto)
        {
            if (callDto == null) return;

            var callId = callDto.CallId;
            var callType = callDto.CallType;
            var callerId = callDto.CallerId;
            var callerPlatform = callDto.CallerPlatform;
            var calleeId = callDto.CalleeId;
            var calleePlatform = callDto.CalleePlatform;
            var create = callDto.CreateAt;
            var expiry = callDto.ExpiryAt;


            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            #region 通知呼叫方在线设备
            var callerConnectionId = await _redisConnectionService.GetConnectionIdAsync(callerId, callerPlatform);
            if (!string.IsNullOrEmpty(callerConnectionId)) await _chatHub.Clients.Client(callerConnectionId!).SendAsync("CallRejected", callId, parallelOptions.CancellationToken);
            #endregion

            #region 通知被呼叫方其他在线设备
            var calleeConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(calleeId, calleePlatform);
            await Parallel.ForEachAsync(calleeConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("CallHandled", callId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "[RejectCallNotification] : ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }

        public async Task HangUpCallNotify(CallInfo callDto, long currentUserId)
        {
            if (callDto == null) return;

            var callId = callDto.CallId;
            var callType = callDto.CallType;
            var callerId = callDto.CallerId;
            var callerPlatform = callDto.CallerPlatform;
            var calleeId = callDto.CalleeId;
            var calleePlatform = callDto.CalleePlatform;
            var create = callDto.CreateAt;
            var expiry = callDto.ExpiryAt;
            var start = callDto.StartAt!.Value;


            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            #region 通知对方在线设备
            var userId = currentUserId == callDto.CallerId ? callDto.CalleeId : callDto.CallerId;
            var userPlatform = currentUserId == callDto.CallerId ? callDto.CalleePlatform : callDto.CallerPlatform;

            var userConnectionId = await _redisConnectionService.GetConnectionIdAsync(userId, userPlatform);
            if (!string.IsNullOrEmpty(userConnectionId)) await _chatHub.Clients.Client(userConnectionId!).SendAsync("CallHangUp", callId, parallelOptions.CancellationToken);
            #endregion
        }

        public async Task AbnormalCallNotify(CallInfo callDto, long currentUserId)
        {
            if (callDto == null) return;

            var callId = callDto.CallId;
            var callType = callDto.CallType;
            var callerId = callDto.CallerId;
            var callerPlatform = callDto.CallerPlatform;
            var calleeId = callDto.CalleeId;
            var calleePlatform = callDto.CalleePlatform;
            var create = callDto.CreateAt;
            var expiry = callDto.ExpiryAt;
            var start = callDto.StartAt!.Value;

            var messageDto = new MessageDto
            {
                Chat_Type = ChatType.Private,
                Sender_Id = callerId,
                Target_Id = calleeId,
                Message_Type = callType == CallType.Video ? MessageType.VideoCall : MessageType.VoiceCall,
                Content = null,
                Extra = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    {"CallId", callId },
                    { "CallStatus", (int)CallStatus.Abnormal },
                    { "Duration", (DateTime.UtcNow - start).TotalSeconds }
                }),
                Message_Status = MessageStatus.Sented,
                Created_At = DateTime.UtcNow
            };

            var result = await _messageService.HandleNewMessage(messageDto);
            if (!result)
            {
                await _messageService.HandleNewMessage(messageDto);
            }

            // 发送消息
            await SendCallMessageNotify(messageDto);


            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            #region 通知对方在线设备
            var userId = currentUserId == callDto.CallerId ? callDto.CalleeId : callDto.CallerId;
            var userPlatform = currentUserId == callDto.CallerId ? callDto.CalleePlatform : callDto.CallerPlatform;

            var userConnectionId = await _redisConnectionService.GetConnectionIdAsync(userId, userPlatform);
            if (!string.IsNullOrEmpty(userConnectionId)) await _chatHub.Clients.Client(userConnectionId!).SendAsync("CallAbnormal", callId, parallelOptions.CancellationToken);
            #endregion
        }

        public async Task ExpiryCallNotify(CallInfo callDto)
        {
            if (callDto == null) return;

            var callId = callDto.CallId;
            var callType = callDto.CallType;
            var callerId = callDto.CallerId;
            var callerPlatform = callDto.CallerPlatform;
            var calleeId = callDto.CalleeId;
            var calleePlatform = callDto.CalleePlatform;
            var create = callDto.CreateAt;
            var expiry = callDto.ExpiryAt;

            var messageDto = new MessageDto
            {
                Chat_Type = ChatType.Private,
                Sender_Id = callerId,
                Target_Id = calleeId,
                Message_Type = callType == CallType.Video ? MessageType.VideoCall : MessageType.VoiceCall,
                Content = null,
                Extra = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    {"CallId", callId },
                    { "CallStatus", (int)CallStatus.Expired },
                    { "Duration", 0 }
                }),
                Message_Status = MessageStatus.Sented,
                Created_At = DateTime.UtcNow
            };

            var result = await _messageService.HandleNewMessage(messageDto);
            if (!result)
            {
                await _messageService.HandleNewMessage(messageDto);
            }

            // 发送消息
            await SendCallMessageNotify(messageDto);


            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            #region 通知呼叫方在线设备
            var callerConnectionId = await _redisConnectionService.GetConnectionIdAsync(callerId, callerPlatform);
            if (!string.IsNullOrEmpty(callerConnectionId)) await _chatHub.Clients.Client(callerConnectionId!).SendAsync("CallExpiry", callId, parallelOptions.CancellationToken);
            #endregion

            #region 通知被呼叫方在线设备
            var calleeConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(calleeId);
            await Parallel.ForEachAsync(calleeConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("CallExpiry", callId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "[ExpiryCallNotification] : ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }

        public async Task SendCallMessageNotify(MessageDto messageDto)
        {
            if (messageDto == null) return;

            // 单人

            var hasSuccess = 0;
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };


            #region 通知对方所有在线设备
            var receiverConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(messageDto.Target_Id);
            await Parallel.ForEachAsync(receiverConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    var success = await _chatHub.Clients.Client(connectionId).InvokeAsync<bool>("ReceiveMessage", messageDto, cancellationToken);
                    if (success) Interlocked.Exchange(ref hasSuccess, 1);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "发送消息失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });

            // 接收成功，更新未读数
            if (hasSuccess == 1)
            {
                await _chatService.ClearChatUnread(messageDto.Target_Id, messageDto.Sender_Id, ChatType.Private);
            }
            #endregion


            #region 通知自己所有在线设备
            hasSuccess = 0;

            var senderPlatformConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(messageDto.Sender_Id);
            await Parallel.ForEachAsync(senderPlatformConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    var success = await _chatHub.Clients.Client(connectionId).InvokeAsync<bool>("ReceiveMessage", messageDto, cancellationToken);
                    if (success) Interlocked.Exchange(ref hasSuccess, 1);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "发送消息失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });

            // 接收成功，更新未读数
            if (hasSuccess == 1)
            {
                await _chatService.ClearChatUnread(messageDto.Sender_Id, messageDto.Target_Id, ChatType.Private);
            }
            #endregion
        }


        public async Task RequestContactNotify(RequestContactReturn result, int platform)
        {
            long currentUserId = result.ContactRequest.Sender_Id;
            long contactId = result.ContactRequest.Receiver_Id;
            ContactRequestDto? contactRequest = result.ContactRequest;
            ContactDto? senderContact = result.SenderContact;
            ChatDto? senderChat = result.SenderChat;
            MessageDto? senderMessage = result.SenderMessage;
            ContactDto? receiverContact = result.ReceiverContact;
            ChatDto? receiverChat = result.ReceiverChat;
            MessageDto? receiverMessage = result.ReceiverMessage;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };


            #region 通知自己其他在线设备
            var otherPlatformConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(currentUserId, platform);
            await Parallel.ForEachAsync(otherPlatformConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("RequestContact", contactRequest, senderContact, senderChat, senderMessage, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "请求联系人同步失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion


            #region 通知对方所有在线设备
            var hasSuccess = 0;
            var contactConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(contactId);
            await Parallel.ForEachAsync(contactConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    var success = await _chatHub.Clients.Client(connectionId).InvokeAsync<bool>("RequestContact", contactRequest, receiverContact, receiverChat, receiverMessage, cancellationToken);
                    if (success) Interlocked.Exchange(ref hasSuccess, 1);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "请求联系人失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });

            // 接收成功，更新未读数
            if (hasSuccess == 1)
            {
                await _chatService.ClearChatUnread(result.ReceiverMessage!.Sender_Id, result.ReceiverMessage!.Target_Id, ChatType.Private);
            }
            #endregion
        }

        public async Task RespondContactNotify(RespondContactHandle handle, RespondContactReturn result, int platform)
        {
            long currentUserId = 0;
            long contactId = 0;
            ContactRequestDto? contactRequest = null;
            ContactDto? senderContact = null;
            ChatDto? senderChat = null;
            MessageDto? senderMessage = null;
            ContactDto? receiverContact = null;
            ChatDto? receiverChat = null;
            MessageDto? receiverMessage = null;

            if (handle == RespondContactHandle.Revoked)
            {
                currentUserId = result.ContactRequest.Sender_Id;
                contactId = result.ContactRequest.Receiver_Id;
                contactRequest = result.ContactRequest;
            }
            else if (handle == RespondContactHandle.Declined)
            {
                currentUserId = result.ContactRequest.Receiver_Id;
                contactId = result.ContactRequest.Sender_Id;
                contactRequest = result.ContactRequest;
            }
            else if (handle == RespondContactHandle.Accepted)
            {
                currentUserId = result.ContactRequest.Receiver_Id;
                contactId = result.ContactRequest.Sender_Id;
                contactRequest = result.ContactRequest;

                senderContact = result.SenderContact;
                senderChat = result.SenderChat;
                senderMessage = result.SenderMessage;

                receiverContact = result.ReceiverContact;
                receiverChat = result.ReceiverChat;
                receiverMessage = result.ReceiverMessage;
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };


            #region 通知自己其他在线设备
            var otherPlatformConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(currentUserId, platform);
            await Parallel.ForEachAsync(otherPlatformConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("RespondContact", contactRequest, receiverContact, receiverChat, receiverMessage, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "回复联系人同步失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion


            #region 通知对方所有在线设备
            var contactConnectionIds = await _redisConnectionService.GetAllPlatformConnectionIdsAsync(contactId);
            await Parallel.ForEachAsync(contactConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("RespondContact", contactRequest, senderContact, senderChat, senderMessage, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "回复联系人失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }

        public async Task DeleteContactNotify(long currentUserId, long contactId, int platform)
        {
            #region 通知自己其他在线设备
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            var otherPlatformConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(currentUserId, platform);
            await Parallel.ForEachAsync(otherPlatformConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("DeleteContact", contactId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "删除联系人失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }


        public async Task UpdateHeadNotify(long currentUserId, string url, int platform)
        {
            #region 通知自己其他在线设备
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            var otherPlatformConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(currentUserId, platform);
            await Parallel.ForEachAsync(otherPlatformConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("UpdateHead", url, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "更新头像失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }

        public async Task UpdateProfileNotify(long currentUserId, UpdateUserRequest updateUserRequest, int platform)
        {
            #region 通知自己其他在线设备
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = CancellationToken.None
            };

            var otherPlatformConnectionIds = await _redisConnectionService.GetOtherPlatformConnectionIdsAsync(currentUserId, platform);
            await Parallel.ForEachAsync(otherPlatformConnectionIds, parallelOptions, async (connectionId, cancellationToken) =>
            {
                try
                {
                    await _chatHub.Clients.Client(connectionId).SendAsync("UpdateProfile", updateUserRequest, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 记录日志
                    // _logger.LogError(ex, "更新个人信息失败，ConnectionId: {ConnectionId}", connectionId);
                }
            });
            #endregion
        }

    }
}
