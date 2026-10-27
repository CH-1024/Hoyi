using HY.MAUI.Communication.Http.Requests;
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
        Task<bool> RefreshTokenAsync();
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


        public async Task<bool> RefreshTokenAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
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

                if (!resp.IsSuccessStatusCode) return false;

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
