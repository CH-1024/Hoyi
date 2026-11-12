using Microsoft.AspNetCore.SignalR.Client;

namespace HY.MAUI.Services
{
    public sealed class SignalRRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            var seconds = Math.Min(60, Math.Pow(2, Math.Min(retryContext.PreviousRetryCount, 6)));
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
