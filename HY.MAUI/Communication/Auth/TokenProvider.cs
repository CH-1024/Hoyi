using HY.MAUI.Communication;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Communication.Auth
{
    public interface ITokenProvider
    {
        Task<string?> GetAccessTokenAsync();
        Task<string?> GetRefreshTokenAsync();
        //Task<DateTime?> GetAccessTokenExpiryAsync();

        Task<bool> IsAccessTokenExpired();

        Task SetAsync(TokenModel token);
        void Clear();
    }


    public class TokenProvider : ITokenProvider
    {
        private const string AccessKey = "hy_access_token";
        private const string RefreshKey = "hy_refresh_token";
        private const string AccessExpiryKey = "hy_access_token_expiry";



        public async Task<string?> GetAccessTokenAsync() => await SecureStorage.GetAsync(AccessKey);

        public async Task<string?> GetRefreshTokenAsync() => await SecureStorage.GetAsync(RefreshKey);

        public async Task<bool> IsAccessTokenExpired()
        {
            var expiry = await GetAccessTokenExpiryAsync();
            if (expiry.HasValue)
            {
                return DateTime.UtcNow >= expiry.Value.AddSeconds(-30);
            }
            return true;
        }


        public async Task SetAsync(TokenModel token)
        {
            await SecureStorage.SetAsync(AccessKey, token.AccessToken!);
            await SecureStorage.SetAsync(RefreshKey, token.RefreshToken!);
            await SecureStorage.SetAsync(AccessExpiryKey, token.AccessExpiresAt.ToString("o"));
        }


        public void Clear()
        {
            SecureStorage.Remove(AccessKey);
            SecureStorage.Remove(RefreshKey);
            SecureStorage.Remove(AccessExpiryKey);
        }




        private async Task<DateTime?> GetAccessTokenExpiryAsync()
        {
            var expiryString = await SecureStorage.GetAsync(AccessExpiryKey);
            if (DateTime.TryParse(expiryString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiry))
            {
                return expiry;
            }
            return null;
        }


    }
}
