using System.Text.Json.Serialization;

namespace HY.ApiService.Models
{
    public class FileChallengeInfo
    {
        public required string ChallengeId { get; init; }

        public required long UserId { get; init; }

        public required long StorageId { get; init; }

        /// <summary>验证块序号与对应的 MD5（十六进制字符串）</summary>
        public required Dictionary<long, string> BlockMd5s { get; init; }
    }
}
