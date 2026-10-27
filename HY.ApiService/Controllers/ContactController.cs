using HY.ApiService.Dtos;
using HY.ApiService.Enums;
using HY.ApiService.Hubs;
using HY.ApiService.Models;
using HY.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace HY.ApiService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ContactController : ControllerBase
    {
        readonly IChatNotificationService _chatNotificationService;

        readonly IContactService _contactService;


        public ContactController(IChatNotificationService chatNotificationService, IContactService contactService)
        {
            _chatNotificationService = chatNotificationService;

            _contactService = contactService;
        }


        [Authorize]
        [HttpGet("get/contactrequests")]
        public async Task<IActionResult> GetContactRequests()
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var result = await _contactService.GetAllContactRequestsByUserId(userId);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    { "ContactRequests", result.ContactRequests },
                }
            });
        }

        [Authorize]
        [HttpGet("get/contacts")]
        public async Task<IActionResult> GetContacts()
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var result = await _contactService.GetAllContactsByUserId(userId);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    { "Contacts", result.Contacts },
                }
            });
        }

        [Authorize]
        [HttpGet("get/contact")]
        public async Task<IActionResult> GetContact(long targetId)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var result = await _contactService.GetContactByUserId(userId, targetId);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    { "Contact", result.Contact },
                }
            });
        }

        [Authorize]
        [HttpGet("search/contact")]
        public async Task<IActionResult> SearchContact(string identity)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var result = await _contactService.GetContactByHYidOrPhone(userId, identity);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    { "Contact", result.Contact },
                }
            });
        }

        [Authorize]
        [HttpPost("request/contact")]
        public async Task<IActionResult> RequestContact(long contactId, int source, string? message)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            var result = await _contactService.RequestContact(userId, contactId, source, message);
            if (!result.IsSucc) return Ok(new Response(false, result.Error));

            // 通知接收方
            await _chatNotificationService.RequestContactNotify(result!, platform);

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "ContactRequest", result.ContactRequest },
                    { "Contact", result.SenderContact },
                    { "Chat", result.SenderChat },
                    { "Message", result.SenderMessage },
                }
            });
        }

        [Authorize]
        [HttpPost("respond/contact")]
        public async Task<IActionResult> RespondContact(long contactRequestId, RespondContactHandle handle)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            var result = await _contactService.RespondContact(userId, contactRequestId, handle);
            if (!result.IsSucc) return Ok(new Response(false, result.Error));

            // 通知接收方
            await _chatNotificationService.RespondContactNotify(handle, result!, platform);

            ContactRequestDto? contactRequest = null;
            ContactDto? receiverContact = null;
            ChatDto? receiverChat = null;
            MessageDto? receiverMessage = null;

            if (handle == RespondContactHandle.Revoked)
            {
                contactRequest = result.ContactRequest;
            }
            else if (handle == RespondContactHandle.Declined)
            {
                contactRequest = result.ContactRequest;
            }
            else if (handle == RespondContactHandle.Accepted)
            {
                contactRequest = result.ContactRequest;
                receiverContact = result.ReceiverContact;
                receiverChat = result.ReceiverChat;
                receiverMessage = result.ReceiverMessage;
            }

            return Ok(new Response(true)
            {
                Data = new Dictionary<string, object?>
                {
                    { "ContactRequest", contactRequest },
                    { "Contact", receiverContact },
                    { "Chat", receiverChat },
                    { "Message", receiverMessage },
                }
            });
        }

        [Authorize]
        [HttpDelete("delete/contact")]
        public async Task<IActionResult> DeleteContact(long targetId)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            var result = await _contactService.DeleteContact(userId, targetId);
            if (result?.IsSucc != true) return Ok(new Response(false, result?.Error));

            // 通知接收方
            await _chatNotificationService.DeleteContactNotify(userId, targetId, platform);

            return Ok(new Response(result.IsSucc, result.Error));
        }
    }
}
