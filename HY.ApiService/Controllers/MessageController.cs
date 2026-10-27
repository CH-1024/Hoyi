using HY.ApiService.Dtos;
using HY.ApiService.Enums;
using HY.ApiService.Hubs;
using HY.ApiService.Models;
using HY.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using SqlSugar;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace HY.ApiService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessageController : ControllerBase
    {
        readonly IChatNotificationService _chatNotificationService;

        readonly IChatService _chatService;
        readonly IMessageService _messageService;
        readonly IGroupMemberService _groupMemberService;
        readonly IContactService _contactService;


        public MessageController(IChatNotificationService chatNotificationService, IChatService chatService, IMessageService messageService, IGroupMemberService groupMemberService, IContactService contactService)
        {
            _chatNotificationService = chatNotificationService;

            _chatService = chatService;
            _messageService = messageService;
            _groupMemberService = groupMemberService;
            _contactService = contactService;
        }



        [Authorize]
        [HttpGet("gets")]
        public async Task<IActionResult> GetMessages(long chatId, long skipMessageId, int take)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var bol = await _chatService.IsUserOwnerChat(userId, chatId);
            if (!bol)
            {
                return Ok(new Response(false, "没有权限"));
            }

            var messages = await _messageService.GetMessagesByChatId(chatId, skipMessageId, take);

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "Messages",  messages}
                }
            });
        }


        [Authorize]
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] MessageDto messageDto)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            // Todo: 黑名单验证

            if (messageDto == null)
            {
                return Ok(new Response(false, "消息内容不能为空"));
            }
            if (messageDto.Sender_Id != userId)
            {
                return Ok(new Response(false, "发送者ID不合法"));
            }
            if (messageDto.Chat_Type == ChatType.Private)
            {
                // 私聊
                // 联系人验证
                var contactResult = await _contactService.GetContactByUserId(messageDto.Target_Id, messageDto.Sender_Id);
                if (!contactResult.IsSucc)
                {
                    return Ok(new Response(false, contactResult.Error));
                }
                else if (contactResult.Contact!.Relation_Status != RelationStatus.Friend)
                {
                    return Ok(new Response(false, "不是好友关系"));
                }
            }
            else if (messageDto.Chat_Type == ChatType.Group)
            {
                // 群聊
                // 群成员验证
                var groupMemberResult = await _groupMemberService.GetGroupMember(messageDto.Target_Id, messageDto.Sender_Id);
                if (groupMemberResult == null)
                {
                    return Ok(new Response(false, "不是群成员"));
                }
            }
            else
            {
                return Ok(new Response(false, "无效的聊天类型"));
            }

            var result = await _messageService.HandleNewMessage(messageDto);
            if (!result)
            {
                return Ok(new Response(false, "事务处理失败"));
            }

            // 通知接收方
            await _chatNotificationService.SendMessageNotify(messageDto, platform);

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "MessageId", messageDto.Id },
                    { "CreatedAt", messageDto.Created_At },
                }
            });
        }

        [Authorize]
        [HttpPost("recall")]
        public async Task<IActionResult> RecallMessage(long messageId)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            var messageDto = await _messageService.GetMessageById(userId, messageId);
            if (messageDto == null) return Ok(new Response(false, "消息不存在"));

            // 只有未撤回的消息才能被撤回
            if (messageDto.Message_Status == MessageStatus.Recalled) return Ok(new Response(false, "消息已撤回"));

            // 只有发送者才能撤回消息
            if (messageDto.Sender_Id != userId) return Ok(new Response(false, "只有发送者才能撤回消息"));

            // 只有在规定时间内才能撤回消息（5分钟内）
            if ((DateTime.UtcNow - messageDto.Created_At).TotalMinutes > 5) return Ok(new Response(false, "超过撤回时间限制"));

            // 撤回消息
            var result = await _messageService.RecallMessage(messageId);
            if (!result) return Ok(new Response(false, "撤回消息失败"));

            // 通知接收方
            await _chatNotificationService.RecallMessageNotify(messageDto, platform);

            return Ok(new Response(true));
        }

        [Authorize]
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteMessage(long messageId)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            var messageDto = await _messageService.GetMessageById(userId, messageId);
            if (messageDto == null) return Ok(new Response(false, "消息不存在"));

            var result = await _messageService.InsertMessageAction(userId, messageId, MessageActionType.Delete);
            if (!result) return Ok(new Response(false, "删除消息失败"));

            // 通知接收方
            await _chatNotificationService.DeleteMessageNotify(userId, messageDto, platform);

            return Ok(new Response(true));
        }




        //[Authorize]
        //[HttpGet("get/single/messages")]
        //public async Task<IActionResult> GetSingleChatMessages(long userId1, long userId2, int skip, int take)
        //{
        //    var messages = await _messageService.GetPrivateChatMessages(userId1, userId2, skip, take);
        //    return Ok(new Response(true)
        //    {
        //        Data = new Dictionary<string, object?>
        //        {
        //            { "Messages",  messages}
        //        }
        //    });
        //}


        //[Authorize]
        //[HttpGet("get/group/messages")]
        //public async Task<IActionResult> GetGroupChatMessages(long groupId, int skip, int take)
        //{
        //    var messages = await _messageService.GetGroupChatMessages(groupId, skip, take);
        //    return Ok(new Response(true)
        //    {
        //        Data = new Dictionary<string, object?>
        //        {
        //            { "Messages",  messages}
        //        }
        //    });
        //}

    }
}
