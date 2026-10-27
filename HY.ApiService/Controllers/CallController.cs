using HY.ApiService.Dtos;
using HY.ApiService.Enums;
using HY.ApiService.Hubs;
using HY.ApiService.Hubs.Requests;
using HY.ApiService.Models;
using HY.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace HY.ApiService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CallController : ControllerBase
    {
        readonly IRedisCallService _redisCallService;
        readonly IRedisConnectionService _redisConnectionService;
        readonly IChatNotificationService _chatNotificationService;

        readonly IUserService _userService;
        readonly IMessageService _messageService;
        readonly IChatService _chatService;
        readonly IContactService _contactService;
        readonly IGroupMemberService _groupMemberService;


        public CallController(IRedisCallService redisCallService, IRedisConnectionService redisConnectionService, IChatNotificationService chatNotificationService, IUserService userService, IMessageService messageService, IChatService chatService, IContactService contactService, IGroupMemberService groupMemberService)
        {
            _redisCallService = redisCallService;
            _redisConnectionService = redisConnectionService;
            _chatNotificationService = chatNotificationService;

            _userService = userService;
            _messageService = messageService;
            _chatService = chatService;
            _contactService = contactService;
            _groupMemberService = groupMemberService;
        }



        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateCall([FromBody] CreateCallRequest request)
        {
            var callerId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var callerPlatform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            var callType = request.CallType;
            var calleeId = request.CalleeId;

            if (callerId == calleeId)
            {
                return Ok(new Response(false, "不能呼叫自己"));
            }

            // 联系人验证
            var contactResult = await _contactService.GetContactByUserId(calleeId, callerId);
            if (!contactResult.IsSucc)
            {
                return Ok(new Response(false, contactResult.Error));
            }
            else if (contactResult.Contact!.Relation_Status != RelationStatus.Friend)
            {
                return Ok(new Response(false, "不是好友关系"));
            }

            // 联系人不在线
            var isOnline = await _redisConnectionService.IsUserOnlineAsync(calleeId);
            if (!isOnline)
            {
                return Ok(new Response(false, "对方不在线"));
            }

            var callDto = await _redisCallService.CreateCall(callType, callerId, callerPlatform, calleeId);
            if (callDto == null)
            {
                return Ok(new Response(false, "创建通话失败"));
            }

            // 通知接收方
            await _chatNotificationService.CreateCallNotify(callDto);

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "CallId",  callDto.CallId},
                }
            });
        }

        [Authorize]
        [HttpPost("accept")]
        public async Task<IActionResult> AcceptCall(string callId)
        {
            var calleeId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var calleePlatform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            var callDto = await _redisCallService.AcceptCall(callId, calleeId, calleePlatform);
            if (callDto == null)
            {
                return Ok(new Response(false, "接听通话失败"));
            }

            // 通知接收方
            await _chatNotificationService.AcceptCallNotify(callDto!);

            return Ok(new Response(true));
        }

        [Authorize]
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelCall(string callId)
        {
            var callerId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var callerPlatform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            var callDto = await _redisCallService.CancelCall(callId, callerId);
            if (callDto == null)
            {
                return Ok(new Response(false, "取消通话失败"));
            }

            var messageDto = new MessageDto
            {
                Chat_Type = ChatType.Private,
                Sender_Id = callDto.CallerId,
                Target_Id = callDto.CalleeId,
                Message_Type = callDto.CallType == CallType.Video ? MessageType.VideoCall : MessageType.VoiceCall,
                Content = null,
                Extra = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    { "CallId", callId },
                    { "CallStatus", (int)CallStatus.Cancelled },
                    { "Duration", 0 }
                }),
                Message_Status = MessageStatus.Sented,
                Created_At = DateTime.UtcNow
            };

            var result = await _messageService.HandleNewMessage(messageDto);
            if (!result)
            {
                return Ok(new Response(false, "事务处理失败"));
            }

            // 通知消息
            await _chatNotificationService.SendCallMessageNotify(messageDto);

            // 通知接收方
            await _chatNotificationService.CancelCallNotify(callDto!);

            return Ok(new Response(true));
        }

        [Authorize]
        [HttpPost("reject")]
        public async Task<IActionResult> RejectCall(string callId)
        {
            var calleeId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var calleePlatform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            var callDto = await _redisCallService.RejectCall(callId, calleeId, calleePlatform);
            if (callDto == null)
            {
                return Ok(new Response(false, "拒绝通话失败"));
            }

            var messageDto = new MessageDto
            {
                Chat_Type = ChatType.Private,
                Sender_Id = callDto.CallerId,
                Target_Id = calleeId,
                Message_Type = callDto.CallType == CallType.Video ? MessageType.VideoCall : MessageType.VoiceCall,
                Content = null,
                Extra = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    {"CallId", callId },
                    { "CallStatus", (int)CallStatus.Rejected },
                    { "Duration", 0 }
                }),
                Message_Status = MessageStatus.Sented,
                Created_At = DateTime.UtcNow
            };

            var result = await _messageService.HandleNewMessage(messageDto);
            if (!result)
            {
                return Ok(new Response(false, "事务处理失败"));
            }

            // 通知消息
            await _chatNotificationService.SendCallMessageNotify(messageDto);

            // 通知接收方
            await _chatNotificationService.RejectCallNotify(callDto!);

            return Ok(new Response(true));
        }

        [Authorize]
        [HttpPost("hangup")]
        public async Task<IActionResult> HangUpCall(string callId)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var callDto = await _redisCallService.HangUpCall(callId, userId);
            if (callDto == null)
            {
                return Ok(new Response(false, "挂断通话失败"));
            }

            var messageDto = new MessageDto
            {
                Chat_Type = ChatType.Private,
                Sender_Id = callDto.CallerId,
                Target_Id = callDto.CalleeId,
                Message_Type = callDto.CallType == CallType.Video ? MessageType.VideoCall : MessageType.VoiceCall,
                Content = null,
                Extra = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    {"CallId", callId },
                    { "CallStatus", (int)CallStatus.Ended },
                    { "Duration", (DateTime.UtcNow - callDto.StartAt!.Value).TotalSeconds }
                }),
                Message_Status = MessageStatus.Sented,
                Created_At = DateTime.UtcNow
            };

            var result = await _messageService.HandleNewMessage(messageDto);
            if (!result)
            {
                return Ok(new Response(false, "事务处理失败"));
            }

            // 通知消息
            await _chatNotificationService.SendCallMessageNotify(messageDto);

            // 通知接收方
            await _chatNotificationService.HangUpCallNotify(callDto!, userId);

            return Ok(new Response(true));
        }

        [Authorize]
        [HttpPost("connected")]
        public async Task CallConnected(string callId)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            await _redisCallService.CallConnected(callId, userId);
        }




    }
}
