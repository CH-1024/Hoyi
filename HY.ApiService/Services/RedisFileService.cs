using HY.ApiService.Enums;
using HY.ApiService.Extensions;
using HY.ApiService.Models;
using HY.ApiService.Tools;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using StackExchange.Redis;
using System.Text.Json;

namespace HY.ApiService.Services
{
    public interface IRedisFileService
    {
        Task<FileChallengeInfo?> CreateFileChallenge(long userId, long storageId, FileChallenge fileChallenge);
        Task<long?> ConfirmFileChallenge(long userId, string challengeId, Dictionary<long, string> blockMd5s);
    }


    public class RedisFileService : IRedisFileService
    {
        private readonly IConfiguration _configuration;
        private readonly IRedisBaseService _redis;


        public RedisFileService(IConfiguration configuration, IRedisBaseService redis)
        {
            _configuration = configuration;
            _redis = redis;
        }


        private static string FileChallengeKey(string challengeId)
        {
            return $"FileChallenge:{challengeId}";
        }


        public async Task<FileChallengeInfo?> CreateFileChallenge(long userId, long storageId, FileChallenge fileChallenge)
        {
            var sec = _configuration.GetSection("Expire")?.GetValue<double>("FileChallenge") ?? 600;

            var challengeInfo = new FileChallengeInfo
            {
                ChallengeId = Guid.NewGuid().ToString("N"),
                UserId = userId,
                StorageId = storageId,
                BlockMd5s = fileChallenge.BlockMd5s,
            };

            var challengeKey = FileChallengeKey(challengeInfo.ChallengeId);

            var challengeValue = JsonSerializer.Serialize(challengeInfo);

            var expiry = DateTime.UtcNow.AddSeconds(sec) - DateTime.UtcNow;

            await _redis.StringSetAsync(challengeKey, challengeValue, expiry);

            return challengeInfo;
        }

        public async Task<long?> ConfirmFileChallenge(long userId, string challengeId, Dictionary<long, string> blockMd5s)
        {
            var challengeInfo = await GetFileChallenge(challengeId);
            if (challengeInfo == null) return null;
            if (challengeInfo.UserId != userId) return null;

            var expectedMd5s = challengeInfo.BlockMd5s;
            if (!expectedMd5s.DictionaryEquals(blockMd5s))
            {
                return null;
            }

            await RemoveAsync(challengeId);

            return challengeInfo.StorageId;
        }





        private async Task<FileChallengeInfo?> GetFileChallenge(string challengeId)
        {
            var challengeKey = FileChallengeKey(challengeId);

            var value = await _redis.StringGetAsync(challengeKey);

            if (string.IsNullOrWhiteSpace(value)) return null;

            return JsonSerializer.Deserialize<FileChallengeInfo?>(value);
        }

        private Task<bool> RemoveAsync(string challengeId)
        {
            var challengeKey = FileChallengeKey(challengeId);

            return _redis.KeyDeleteAsync(challengeKey);
        }

    }

}
