using Azure.Core;
using HY.ApiService.Controllers.Requests;
using HY.ApiService.Dtos;
using HY.ApiService.Entities;
using HY.ApiService.Enums;
using HY.ApiService.Models;
using HY.ApiService.Repositories;
using HY.ApiService.Services;
using HY.ApiService.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HY.ApiService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        readonly IUserService _userService;
        readonly ILoginService _loginService;


        public AuthController(IUserService userService, ILoginService loginService)
        {
            _userService = userService;
            _loginService = loginService;
        }





        [AllowAnonymous]
        [HttpGet("ping1")]
        public async Task<IActionResult> Ping1()
        {
            return Ok(new Response(true, "OK"));
        }

        [AllowAnonymous]
        [HttpPost("ping2")]
        public async Task<IActionResult> Ping2()
        {
            return Ok(new Response(true, "OK"));
        }


        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest param)
        {
            var result = await _loginService.Refresh(param);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    //{ "User", result.user },
                    { "Tokens", result.Token },
                }
            });
        }


        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest param)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

            var result = await _loginService.Login(ip, param);

            return Ok(new Response(result.IsSucc, result.Error)
            {
                Data = new Dictionary<string, object?>
                {
                    { "User", result.User },
                    { "Tokens", result.Token },
                }
            });
        }


        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest param)
        {
            var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var deviceId = User.FindFirst("DeviceId")?.Value!;

            var result = await _loginService.Logout(userId, deviceId);

            return Ok(new Response(result));
        }

    }
}
