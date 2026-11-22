using HY.ApiService.Dtos;
using HY.ApiService.Models;
using HY.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HY.ApiService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class SyncController : ControllerBase
    {
        readonly IChatService _chatService;
        readonly IMessageService _messageService;

        readonly ISyncEventService _syncEventService;

        public SyncController(IChatService chatService, IMessageService messageService, ISyncEventService syncEventService)
        {
            _chatService = chatService;
            _messageService = messageService;

            _syncEventService = syncEventService;
        }


        [Authorize]
        [HttpGet("head")]
        public async Task<IActionResult> GetHead()
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var lastEventId = await _syncEventService.GetLatestEventIdAsync(userId);

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "LastEventId", lastEventId }
                }
            });
        }

        [Authorize]
        [HttpGet("changes")]
        public async Task<IActionResult> GetChanges(long afterId, int take)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            take = Math.Clamp(take, 1, 100);
            var events = await _syncEventService.GetEventsAfterAsync(userId, afterId, take);

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "Events", events }
                }
            });
        }

        [Authorize]
        [HttpGet("message")]
        public async Task<IActionResult> GetMessage(long chatId, long messageId)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (!await _chatService.IsUserOwnerChat(userId, chatId)) return Ok(new Response(false, "没有权限"));
            if (!await _chatService.IsChatOwnerMessage(chatId, messageId)) return Ok(new Response(false, "没有权限"));

            var message = await _messageService.GetMessageById(userId, messageId);

            if (message == null) return Ok(new Response(false, "消息不存在、无权访问或已对当前用户删除"));

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "Message", message }
                }
            });
        }



        //[HttpGet("message")]
        //public async Task<IActionResult> SyncMessage(long chatId, long afterMessageId, int take)
        //{
        //    var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        //    if (!await _chatService.IsUserOwnerChat(userId, chatId)) return Ok(new Response(false, "没有权限"));
        //    if (!await _chatService.IsChatOwnerMessage(chatId, afterMessageId)) return Ok(new Response(false, "没有权限"));

        //    take = Math.Clamp(take, 1, 100);
        //    var messages = await _messageService.GetMessagesAfterByChatId(chatId, afterMessageId, take);

        //    return Ok(new Response(true)
        //    {
        //        Data = new Dictionary<string, object?>
        //        {
        //            { "Messages", messages }
        //        }
        //    });
        //}

    }
}
