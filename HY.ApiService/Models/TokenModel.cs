using System;
using System.Collections.Generic;
using System.Text;

namespace HY.ApiService.Models
{
    public sealed class TokenModel
    {
        public required string AccessToken { get; init; }
        public required string RefreshToken { get; init; }
        public required DateTime AccessExpiresAt { get; init; }
    }
}
