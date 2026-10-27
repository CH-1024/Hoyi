using HY.ApiService.Enums;

namespace HY.ApiService.Controllers.Requests
{
    public class ChallengeConfirmRequest
    {
        public required string ChallengeId { get; set; }
        public required string FileName { get; set; }
        public required FileType FileType { get; set; }
        public required Dictionary<long, string> BlockMd5s { get; set; }
    }
}
