using HY.MAUI.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Communication.Http.Requests
{
    public class ChallengeConfirmRequest
    {
        public required string ChallengeId { get; init; }
        public required string FileName { get; init; }
        public required FileType FileType { get; init; }
        public required Dictionary<long, string> BlockMd5s { get; init; }
    }
}
