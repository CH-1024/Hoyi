using HY.ApiService.Enums;
using System.Runtime.CompilerServices;

namespace HY.ApiService.Models
{
    public class CallInfo
    {
        public required string CallId { get; init; }

        public required CallType CallType { get; init; }

        public required long CallerId { get; init; }
        public required int CallerPlatform { get; init; }

        public required long CalleeId { get; init; }
        public int CalleePlatform { get; set; }

        public CallStatus CallState { get; set; }

        public required DateTime CreateAt { get; init; }
        public required DateTime ExpiryAt { get; init; }
        public DateTime? StartAt { get; set; }
    }
}
