using HY.ApiService.Dtos;
using HY.ApiService.Models;
using HY.ApiService.Repositories;
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

        public SyncController(ISyncEventRepository syncEventRepository)
        {
            _syncEventRepository = syncEventRepository;
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
                Id = e.Id,
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
    }
}
