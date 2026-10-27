using HY.ApiService.Controllers.Requests;
using HY.ApiService.Enums;
using HY.ApiService.Models;
using HY.ApiService.Services;
using HY.ApiService.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using System.Security.Claims;

namespace HY.ApiService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        readonly IChatNotificationService _chatNotificationService;

        readonly IUserService _userService;


        public UserController(IChatNotificationService chatNotificationService, IUserService userService)
        {
            _chatNotificationService = chatNotificationService;
            _userService = userService;
        }


        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserRequest registerRequest)
        {
            var result = await _userService.CreateUser(registerRequest);

            return Ok(new Response(result.IsSucc, result.Error));
        }

        [Authorize]
        [HttpPost("update/head")]
        public async Task<IActionResult> UpdateHead(string url)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            if (!RegexHelper.IsValidUrl(url))
            {
                return Ok(new Response(false, "头像 URL 无效"));
            }

            var result = await _userService.UpdateHead(userId, url);
            if (!result.IsSucc) return Ok(new Response(false, result.Error));

            // 通知接收方
            await _chatNotificationService.UpdateHeadNotify(userId, url, platform);

            return Ok(new Response(result.IsSucc));
        }

        [Authorize]
        [HttpPost("update/profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserRequest updateUserRequest)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(User.FindFirst("DevicePlatform")!.Value);

            if (!RegexHelper.IsValidNickname(updateUserRequest.Nickname))
            {
                return Ok(new Response(false, "昵称格式不正确"));
            }
            else if (!RegexHelper.IsValidPhone(updateUserRequest.Phone))
            {
                return Ok(new Response(false, "手机号格式不正确"));
            }
            else if (!RegexHelper.IsValidEmail(updateUserRequest.Email))
            {
                return Ok(new Response(false, "邮箱格式不正确"));
            }

            var result = await _userService.UpdateProfile(userId, updateUserRequest);
            if (!result.IsSucc) return Ok(new Response(false, result.Error));

            // 通知接收方
            await _chatNotificationService.UpdateProfileNotify(userId, updateUserRequest, platform);

            return Ok(new Response(result.IsSucc));
        }
    }
}
