using HY.MAUI.Communication.Http.Requests;
using HY.MAUI.Dtos;
using HY.MAUI.Interfaces;
using HY.MAUI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;

namespace HY.MAUI.Communication.Auth
{
    public interface IAuthService
    {
        Task<bool> RefreshTokenAsync(string? failedAccessToken = null);
    }


    public class AuthService : IAuthService
    {
        private readonly ITokenProvider _tokenProvider;
        private readonly ILoginService _loginService;

        private readonly HttpClient _http;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);




        public AuthService(IHttpClientFactory factory, ITokenProvider tokenProvider, ILoginService loginService)
        {
            _http = factory.CreateClient(nameof(AuthService));
            _tokenProvider = tokenProvider;
            _loginService = loginService;
        }


        public async Task<bool> RefreshTokenAsync(string? failedAccessToken = null)
        {
            await _refreshLock.WaitAsync();
            try
            {
                // 必须在获得锁之后重新读取 Token
                var currentAccessToken = await _tokenProvider.GetAccessTokenAsync();
                var isExpired = await _tokenProvider.IsAccessTokenExpired();

                // 如果调用者因为 401 请求刷新，
                // 但当前 Token 已经被其他线程更新，则不必再次刷新
                if (!string.IsNullOrEmpty(failedAccessToken) && !string.Equals(currentAccessToken, failedAccessToken, StringComparison.Ordinal) && !isExpired)
                {
                    return true;
                }

                // 普通刷新请求：如果其他线程已经刷新成功，直接返回
                if (string.IsNullOrEmpty(failedAccessToken) && !isExpired)
                {
                    return !string.IsNullOrEmpty(currentAccessToken);
                }

                var deviceId = await _loginService.GetDeviceId();
                var devicePlatform = await _loginService.GetDevicePlatform();
                var refreshToken = await _tokenProvider.GetRefreshTokenAsync();

                if (string.IsNullOrEmpty(refreshToken)) return false;

                var param = new RefreshRequest
                {
                    DeviceId = deviceId,
                    DevicePlatform = devicePlatform,
                    RefreshToken = refreshToken,
                };

                var resp = await _http.PostAsJsonAsync(ApiUrl.Refresh, param);

                resp.EnsureSuccessStatusCode();

                var refreshResp = await resp.Content.ReadFromJsonAsync<Response>();

                if (refreshResp == null || !refreshResp.IsSucc) return false;

                var token = refreshResp.GetValue<TokenModel>("Tokens");

                if (token == null) return false;

                await _tokenProvider.SetAsync(token);

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

    }
}
