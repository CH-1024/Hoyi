using HY.ApiService.Dtos;
using HY.ApiService.Models;
using HY.ApiService.Repositories;
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
        private readonly ISyncEventRepository _syncEventRepository;
        private readonly IMessageService _messageService;

        public SyncController(
            ISyncEventRepository syncEventRepository,
            IMessageService messageService)
        {
            _syncEventRepository = syncEventRepository;
            _messageService = messageService;
        }

        [HttpGet("head")]
        public async Task<IActionResult> GetHead()
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var lastEventId = await _syncEventRepository.GetLatestEventIdAsync(userId);

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "LastEventId", lastEventId }
                }
            });
        }

        [HttpGet("changes")]
        public async Task<IActionResult> GetChanges(long afterId = 0, int take = 100)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            take = Math.Clamp(take, 1, 200);

            var events = await _syncEventRepository.GetEventsAfterAsync(userId, afterId, take);
            var dtos = events.Select(e => new SyncEventDto
            {
                Id = e.User_Event_Id,
                Event_Type = e.Event_Type,
                Entity_Id = e.Entity_Id,
                Related_Id = e.Related_Id,
                Created_At = e.Created_At
            }).ToList();

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "Events", dtos }
                }
            });
        }

        [HttpGet("message")]
        public async Task<IActionResult> GetMessage(long chatId, long messageId)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var message = await _messageService.GetMessageForSync(userId, chatId, messageId);

            if (message == null)
                return Ok(new Response(false, "消息不存在、无权访问或已对当前用户删除"));

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "Message", message }
                }
            });
        }
    }
}
