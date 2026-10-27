using HY.ApiService.Enums;
using HY.ApiService.Models;
using StackExchange.Redis;
using System.Configuration;
using System.Globalization;
using System.Runtime.InteropServices;

namespace HY.ApiService.Services
{
    public interface IRedisCallService
    {
        Task<CallInfo?> GetCallInfo(string callId);
        Task<bool> IsUserCalling(long userId);
        Task<bool> IsUserPlatformCalling(long userId, int platform);

        Task<CallInfo?> CreateCall(CallType callType, long callerId, int callerPlatform, long calleeId);
        Task<CallInfo?> AcceptCall(string callId, long calleeId, int calleePlatform);
        Task<CallInfo?> CancelCall(string callId, long callerId);
        Task<CallInfo?> RejectCall(string callId, long calleeId, int calleePlatform);
        Task<CallInfo?> HangUpCall(string callId, long userId);
        Task<CallInfo?> CallConnected(string callId, long userId);
        Task<CallInfo?> AbnormalCall(long userId);
        Task<CallInfo?> ExpiryCall(string callId);
    }


    public class RedisCallService : IRedisCallService
    {
        private readonly IConfiguration _configuration;
        private readonly IRedisBaseService _redis;


        public RedisCallService(IConfiguration configuration, IRedisBaseService redis)
        {
            _configuration = configuration;
            _redis = redis;
        }


        private static string CallKey(string callId)
        {
            return $"Call:Info:{callId}";
        }

        private static string UserKey(long userId)
        {
            return $"Call:User:{userId}";
        }

        private string ExpireKey(string callId)
        {
            return $"Call:Expire:{callId}";
        }


        public async Task<CallInfo?> GetCallInfo(string callId)
        {
            var callKey = CallKey(callId);

            var redisValues = new RedisValue[]
            {
                "CallType",
                "CallerId",
                "CallerPlatform",
                "CalleeId",
                "CalleePlatform",
                "CallState",
                "CreateAt",
                "ExpiresAt",
                "StartAt"
            };
            var values = await _redis.HashGetAsync(callKey, redisValues);
            if (values.All(v => string.IsNullOrEmpty(v)))
            {
                return null;
            }

            return new CallInfo
            {
                CallId = callId,
                CallType = Enum.Parse<CallType>(values[0]),
                CallerId = long.Parse(values[1]),
                CallerPlatform = int.Parse(values[2]),
                CalleeId = long.Parse(values[3]),
                CalleePlatform = int.Parse(values[4]),
                CallState = Enum.Parse<CallStatus>(values[5]),
                CreateAt = DateTimeOffset.Parse(values[6]).UtcDateTime,
                ExpiryAt = DateTimeOffset.Parse(values[7]).UtcDateTime,
                StartAt = string.IsNullOrEmpty(values[8]) ? null : DateTimeOffset.Parse(values[8]).UtcDateTime
            };
        }

        public async Task<bool> IsUserCalling(long userId)
        {
            var callId = await GetCallId(userId);
            if (string.IsNullOrEmpty(callId))
            {
                return false;
            }

            var callDto = await GetCallInfo(callId);

            return callDto!.CallState == CallStatus.Calling &&
                   callDto!.CallState == CallStatus.Accepted &&
                   callDto!.CallState == CallStatus.Connected;
        }

        public async Task<bool> IsUserPlatformCalling(long userId, int platform)
        {
            var callId = await GetCallId(userId);
            if (string.IsNullOrEmpty(callId))
            {
                return false;
            }

            var callDto = await GetCallInfo(callId);
            if ((callDto!.CallerId == userId && callDto!.CallerPlatform == platform) || 
                (callDto!.CalleeId == userId && callDto!.CalleePlatform == platform))
            {
                return callDto.CallState == CallStatus.Calling ||
                       callDto.CallState == CallStatus.Accepted ||
                       callDto.CallState == CallStatus.Connected;
            }

            return false;
        }




        public async Task<CallInfo?> CreateCall(CallType callType, long callerId, int callerPlatform, long calleeId)
        {
            var sec = _configuration.GetSection("Expire")?.GetValue<double>("Call") ?? 30;

            var callDto = new CallInfo
            {
                CallId = Guid.NewGuid().ToString("N"),

                CallType = callType,

                CallerId = callerId,
                CallerPlatform = callerPlatform,

                CalleeId = calleeId,
                CalleePlatform = -1,        // -1: 未知

                CallState = CallStatus.Calling,

                CreateAt = DateTime.UtcNow,
                ExpiryAt = DateTime.UtcNow.AddSeconds(sec),
                //StartAt = ,
            };

            var callKey = CallKey(callDto.CallId);
            var callerKey = UserKey(callDto.CallerId);
            var calleeKey = UserKey(callDto.CalleeId);
            var expireKey = ExpireKey(callDto.CallId);

            var entries = new HashEntry[]
            {
                new("CallType", callDto.CallType.ToString()),
                new("CallerId", callDto.CallerId),
                new("CallerPlatform", callDto.CallerPlatform),
                new("CalleeId", callDto.CalleeId),
                new("CalleePlatform", callDto.CalleePlatform),
                new("CallState", callDto.CallState.ToString()),
                new("CreateAt", callDto.CreateAt.ToString("O")),
                new("ExpiresAt", callDto.ExpiryAt.ToString("O")),
                //new("StartAt", ),
            };

            var transaction = _redis.CreateTransaction();

            // 双方都必须没有正在进行的通话
            transaction.AddCondition(Condition.KeyNotExists(callerKey));
            transaction.AddCondition(Condition.KeyNotExists(calleeKey));

            // 创建通话
            transaction.HashSetAsync(callKey, entries);

            // 建立用户 → CallId 索引
            transaction.StringSetAsync(callerKey, callDto.CallId);
            transaction.StringSetAsync(calleeKey, callDto.CallId);

            // 建立过期索引
            transaction.StringSetAsync(expireKey, callDto.CallId);

            // 设置过期时间
            transaction.KeyExpireAsync(callKey,   callDto.ExpiryAt.AddSeconds(10));  // 在 ExpireKey 过期后10秒自动清理
            transaction.KeyExpireAsync(callerKey, callDto.ExpiryAt.AddSeconds(10));  // 在 ExpireKey 过期后10秒自动清理
            transaction.KeyExpireAsync(calleeKey, callDto.ExpiryAt.AddSeconds(10));  // 在 ExpireKey 过期后10秒自动清理
            transaction.KeyExpireAsync(expireKey, callDto.ExpiryAt);    // ExpireKey 负责触发 expired

            if (!await transaction.ExecuteAsync())
                return null;

            return callDto;
        }

        public async Task<CallInfo?> AcceptCall(string callId, long calleeId, int calleePlatform)
        {
            // callId 必须匹配
            var _callId = await GetCallId(calleeId);
            if (string.IsNullOrEmpty(_callId) || _callId != callId)
                return null;

            // calleeId 必须匹配
            var callDto = await GetCallInfo(callId);
            if (callDto == null || callDto.CalleeId != calleeId)
                return null;

            if (callDto.CallState == CallStatus.Accepted)
                return null;

            var callKey = CallKey(callId);
            var utc = DateTime.UtcNow;

            var entries = new HashEntry[]
            {
                new("CalleePlatform", calleePlatform),
                new("CallState", CallStatus.Accepted.ToString()),
                new("StartAt", utc.ToString("O")),
            };

            if (!await _redis.HashSetAsync(callKey, entries))
                return null;

            callDto.CalleePlatform = calleePlatform;
            callDto.CallState = CallStatus.Accepted;
            callDto.StartAt = utc;

            return callDto;
        }

        public async Task<CallInfo?> CancelCall(string callId, long callerId)
        {
            // callId 必须匹配
            var _callId = await GetCallId(callerId);
            if (string.IsNullOrEmpty(_callId) || _callId != callId)
                return null;

            // callerId 必须匹配
            var callDto = await GetCallInfo(callId);
            if (callDto == null || callDto.CallerId != callerId)
                return null;

            if (callDto.CallState == CallStatus.Cancelled)
                return null;

            var callKey = CallKey(callId);

            if (!await _redis.HashSetAsync(callKey, new HashEntry("CallState", CallStatus.Cancelled.ToString())))
                return null;

            callDto.CallState = CallStatus.Cancelled;

            if (!await EndCall(callId))
                return null;

            return callDto;
        }

        public async Task<CallInfo?> RejectCall(string callId, long calleeId, int calleePlatform)
        {
            // callId 必须匹配
            var _callId = await GetCallId(calleeId);
            if (string.IsNullOrEmpty(_callId) || _callId != callId)
                return null;

            // calleeId 必须匹配
            var callDto = await GetCallInfo(callId);
            if (callDto == null || callDto.CalleeId != calleeId)
                return null;

            if (callDto.CallState == CallStatus.Rejected)
                return null;

            var callKey = CallKey(callId);

            var entries = new HashEntry[]
            {
                new("CalleePlatform", calleePlatform),
                new("CallState", CallStatus.Rejected.ToString())
            };

            if (!await _redis.HashSetAsync(callKey, entries))
                return null;

            callDto.CalleePlatform = calleePlatform;
            callDto.CallState = CallStatus.Rejected;

            if (!await EndCall(callId))
                return null;

            return callDto;
        }

        public async Task<CallInfo?> HangUpCall(string callId, long userId)
        {
            // callId 必须匹配
            var _callId = await GetCallId(userId);
            if (string.IsNullOrEmpty(_callId) || _callId != callId)
                return null;

            // callerId/calleeId 必须匹配
            var callDto = await GetCallInfo(callId);
            if (callDto == null || (callDto.CallerId != userId && callDto.CalleeId != userId))
                return null;

            if (callDto.CallState == CallStatus.Ended)
                return null;

            var callKey = CallKey(callId);

            if(!await _redis.HashSetAsync(callKey, new HashEntry("CallState", CallStatus.Ended.ToString())))
                return null;

            callDto.CallState = CallStatus.Ended;

            if (!await EndCall(callId))
                return null;

            return callDto;
        }

        public async Task<CallInfo?> CallConnected(string callId, long userId)
        {
            // callId 必须匹配
            var _callId = await GetCallId(userId);
            if (string.IsNullOrEmpty(_callId) || _callId != callId)
                return null;

            // callerId/calleeId 必须匹配
            var callDto = await GetCallInfo(callId);
            if (callDto == null || (callDto.CallerId != userId && callDto.CalleeId != userId))
                return null;

            if (callDto.CallState == CallStatus.Connected)
                return null;

            var transaction = _redis.CreateTransaction();

            // 删除过期索引
            transaction.KeyDeleteAsync(ExpireKey(callDto.CallId));

            // 取消过期时间
            transaction.KeyPersistAsync(CallKey(callDto.CallId));
            transaction.KeyPersistAsync(UserKey(callDto.CallerId));
            transaction.KeyPersistAsync(UserKey(callDto.CalleeId));

            if (!await transaction.ExecuteAsync())
                return null;

            var callKey = CallKey(callId);

            if (!await _redis.HashSetAsync(callKey, new HashEntry("CallState", CallStatus.Connected.ToString())))
                return null;

            callDto.CallState = CallStatus.Connected;

            return callDto;
        }

        // 不对外公开，仅 OnDisconnectedAsync 调用
        public async Task<CallInfo?> AbnormalCall(long userId)
        {
            var callId = await GetCallId(userId);

            var callKey = CallKey(callId!);

            var callDto = await GetCallInfo(callId!);
            if (callDto == null)
                return null;

            if (callDto.CallState == CallStatus.Abnormal)
                return null;

            if (!await _redis.HashSetAsync(callKey, new HashEntry("CallState", CallStatus.Abnormal.ToString())))
                return null;

            if (!await EndCall(callId!))
                return null;

            return callDto;
        }

        // 不对外公开，仅 RedisTimeoutService 调用
        public async Task<CallInfo?> ExpiryCall(string callId)
        {
            var callKey = CallKey(callId);

            var callDto = await GetCallInfo(callId);
            if (callDto == null)
                return null;

            if (callDto.CallState == CallStatus.Expired)
                return null;

            if (!await _redis.HashSetAsync(callKey, new HashEntry("CallState", CallStatus.Expired.ToString())))
                return null;

            callDto.CallState = CallStatus.Expired;

            if (!await EndCall(callId))
                return null;

            return callDto;
        }

        


        private async Task<bool> EndCall(string callId)
        {
            var callKey = CallKey(callId);
            var expireKey = ExpireKey(callId);

            var values = await _redis.HashGetAsync(callKey, new RedisValue[] { "CallerId", "CalleeId" });
            if (values.All(v => string.IsNullOrEmpty(v)))
            {
                return false;
            }

            var callerKey = UserKey(long.Parse(values[0]));
            var calleeKey = UserKey(long.Parse(values[1]));

            var transaction = _redis.CreateTransaction();

            // 删除通话记录和索引
            transaction.KeyDeleteAsync(callKey);
            transaction.KeyDeleteAsync(callerKey);
            transaction.KeyDeleteAsync(calleeKey);
            transaction.KeyDeleteAsync(expireKey);

            return await transaction.ExecuteAsync();
        }

        private async Task<string?> GetCallId(long userId)
        {
            var userKey = UserKey(userId);

            return await _redis.StringGetAsync(userKey);
        }

    }
}
