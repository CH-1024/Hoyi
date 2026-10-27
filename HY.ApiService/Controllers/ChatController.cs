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
    public class ChatController : ControllerBase
    {
        readonly IChatNotificationService _chatNotificationService;

        readonly IChatService _chatService;


        public ChatController(IChatNotificationService chatNotificationService, IChatService chatService)
        {
            _chatNotificationService = chatNotificationService;
            _chatService = chatService;
        }



        [Authorize]
        [HttpGet("get/chats")]
        public async Task<IActionResult> GetChats()
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var result = await _chatService.GetAllChatsByUserId(userId);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    { "Chats",  result.Chats }
                }
            });
        }


        [Authorize]
        [HttpPost("read/all")]
        public async Task<IActionResult> ReadAll(long chatId)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            var bol = await _chatService.IsUserOwnerChat(userId, chatId);
            if (!bol)
            {
                return Ok(new Response(false, "没有权限"));
            }

            var result = await _chatService.ClearChatUnread(chatId);
            if (result)
            {
                await _chatNotificationService.ReadAllNotify(userId, chatId, platform);
            }

            return Ok(new Response(result));
        }


    }
}
