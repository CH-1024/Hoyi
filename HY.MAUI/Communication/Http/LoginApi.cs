using HY.MAUI.Communication.Http.Requests;
using HY.MAUI.Configurations;
using HY.MAUI.Interfaces;
using HY.MAUI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace HY.MAUI.Communication.Http
{
    public class LoginApi : BaseApi
    {
        private readonly ILoginService _loginService;

        public LoginApi(HttpClient http, ILoginService loginService) : base(http)
        {
            _loginService = loginService;
        }


        public async Task<bool> Ping()
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(ApiUrl.Address, TimeSpan.FromSeconds(ApiOptions.Timeout));
                    if (reply.Status == IPStatus.Success)
                    {
                        return true;
                    }
                }
            }
            catch (Exception) { }

            return false;
        }

        public async Task<Response?> Ping1()
        {
            return await GetAsync(ApiUrl.Ping1);
        }

        public async Task<Response?> Ping2()
        {
            return await PostAsync(ApiUrl.Ping2, null);
        }

        public async Task<Response?> Register(string nickname, string username, string password, string email, string phone)
        {
            var param = new RegisterRequest
            {
                Nickname = nickname,
                Username = username,
                Password = password,
                Email = email,
                Phone = phone
            };

            return await PostAsJsonAsync(ApiUrl.Register, param);
        }

        public async Task<Response?> Login(string username, string password)
        {
            var param = new LoginRequest
            {
                Username = username,
                Password = password,
                DeviceId = await _loginService.GetDeviceId(),
                DeviceName = await _loginService.GetDeviceName(),
                DevicePlatform = await _loginService.GetDevicePlatform(),
                DeviceType = await _loginService.GetDeviceType()
            };

            return await PostAsJsonAsync(ApiUrl.Login, param);
        }

        public async Task<Response?> Logout()
        {
            var param = new LogoutRequest
            {
            };

            return await PostAsJsonAsync(ApiUrl.Logout, param);
        }

    }
}
