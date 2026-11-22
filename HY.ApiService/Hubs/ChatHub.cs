using Dm.filter;
using HY.ApiService.Dtos;
using HY.ApiService.Entities;
using HY.ApiService.Enums;
using HY.ApiService.Hubs.Requests;
using HY.ApiService.Models;
using HY.ApiService.Repositories;
using HY.ApiService.Services;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.SignalR;
using SqlSugar;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;

namespace HY.ApiService.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        readonly IRedisConnectionService _redisConnectionService;
        readonly IRedisCallService _redisCallService;
        readonly IChatNotificationService _chatNotificationService;
        
        readonly ILoginService _loginService;
        readonly IContactService _contactService;
        readonly IGroupMemberService _groupMemberService;



        public ChatHub(IRedisConnectionService redisConnectionService, IRedisCallService redisCallService, IChatNotificationService chatNotificationService, ILoginService loginService, IContactService contactService, IGroupMemberService groupMemberService)
        {
            _redisConnectionService = redisConnectionService;
            _redisCallService = redisCallService;
            _chatNotificationService = chatNotificationService;

            _loginService = loginService;
            _contactService = contactService;
            _groupMemberService = groupMemberService;
        }


        [Authorize]
        public override async Task OnConnectedAsync()
        {
            var userId = long.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(Context.User!.FindFirst("DevicePlatform")!.Value);

            var isOnline = await _redisConnectionService.IsUserOnlineAsync(userId);
            if (!isOnline)
            {
                await _chatNotificationService.UserOnlineNotify(userId);
            }

            var oldConnectionId = await _redisConnectionService.GetConnectionIdAsync(userId, platform);
            if (!string.IsNullOrEmpty(oldConnectionId) && oldConnectionId != Context.ConnectionId)
            {
                await Clients.Client(oldConnectionId).SendAsync("ForceLogout", "您的账号在其他设备登录了", CancellationToken.None);
            }

            await _redisConnectionService.SetConnectionAsync(userId, platform, Context.ConnectionId);

            await base.OnConnectedAsync();
        }

        [Authorize]
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = long.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var platform = int.Parse(Context.User!.FindFirst("DevicePlatform")!.Value);

            await _redisConnectionService.RemoveConnectionAsync(userId, platform, Context.ConnectionId);

            var isOnline = await _redisConnectionService.IsUserOnlineAsync(userId);
            if (!isOnline)
            {
                await _chatNotificationService.UserOfflineNotify(userId);
            }

            var isCalling = await _redisCallService.IsUserPlatformCalling(userId, platform);
            if (isCalling)
            {
                var callDto = await _redisCallService.AbnormalCall(userId!);

                // 通知接收方
                await _chatNotificationService.AbnormalCallNotify(callDto!, userId);
            }

            await base.OnDisconnectedAsync(exception);
        }


        // InvokeAsync  等待客户端响应  有返回值  同步模式
        // SendAsync    不等待响应      无返回值  异步模式


        [Authorize]
        public async Task SendIce(string callId, string ice)
        {
            var userId = long.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var callInfo = await _redisCallService.GetCallInfo(callId);
            if (callInfo == null)
            {
                return;
            }

            var targetId = userId == callInfo.CalleeId ? callInfo.CallerId : callInfo.CalleeId;
            var targetPlatform = userId == callInfo.CalleeId ? callInfo.CallerPlatform : callInfo.CalleePlatform;

            var connectionId = await _redisConnectionService.GetConnectionIdAsync(targetId, targetPlatform);
            if (!string.IsNullOrEmpty(connectionId)) await Clients.Client(connectionId).SendAsync("ReceiveIce", ice);

            return;
        }

        [Authorize]
        public async Task SendOffer(string callId, string offer)
        {
            var userId = long.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var callInfo = await _redisCallService.GetCallInfo(callId);
            if (callInfo == null)
            {
                return;
            }

            var targetId = userId == callInfo.CalleeId ? callInfo.CallerId : callInfo.CalleeId;
            var targetPlatform = userId == callInfo.CalleeId ? callInfo.CallerPlatform : callInfo.CalleePlatform;

            var connectionId = await _redisConnectionService.GetConnectionIdAsync(targetId, targetPlatform);
            if (!string.IsNullOrEmpty(connectionId)) await Clients.Client(connectionId).SendAsync("ReceiveOffer", offer);

            return;
        }

        [Authorize]
        public async Task SendAnswer(string callId, string answer)
        {
            var userId = long.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var callInfo = await _redisCallService.GetCallInfo(callId);
            if (callInfo == null)
            {
                return;
            }

            var targetId = userId == callInfo.CalleeId ? callInfo.CallerId : callInfo.CalleeId;
            var targetPlatform = userId == callInfo.CalleeId ? callInfo.CallerPlatform : callInfo.CalleePlatform;

            var connectionId = await _redisConnectionService.GetConnectionIdAsync(targetId, targetPlatform);
            if (!string.IsNullOrEmpty(connectionId)) await Clients.Client(connectionId).SendAsync("ReceiveAnswer", answer);

            return;
        }


    }
}
