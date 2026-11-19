using HY.MAUI.Dtos;
using HY.MAUI.Extensions;
using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace HY.MAUI.Services
{
    public class LoginService : ILoginService
    {
        private readonly GlobalBinding _globalBinding;

        private const string LoginKey = "hy_is_login";
        private const string CurrentUserKey = "hy_current_user";
        private const string DeviceIdKey = "hy_device_id";

        public bool IsLoggedIn => SecureStorage.GetAsync(LoginKey).Result == "True";


        public LoginService(GlobalBinding globalBinding)
        {
            _globalBinding = globalBinding;
        }


        public async Task Login(UserVM user)
        {
            await SecureStorage.SetAsync(CurrentUserKey, JsonSerializer.Serialize(user));

            if (_globalBinding.AutoLogin)
            {
                await SecureStorage.SetAsync(LoginKey, "True");
            }
        }

        public void Logout()
        {
            SecureStorage.Remove(LoginKey);
            SecureStorage.Remove(CurrentUserKey);

            _globalBinding.AutoLogin = false;
        }

        public async Task SetCurrentUser(UserVM user)
        {
            await SecureStorage.SetAsync(CurrentUserKey, JsonSerializer.Serialize(user));
        }

        public async Task<UserVM?> GetCurrentUser()
        {
            var userJson = await SecureStorage.GetAsync(CurrentUserKey);
            if (string.IsNullOrEmpty(userJson))
            {
                return null;
            }
            return JsonSerializer.Deserialize<UserVM>(userJson);
        }

        public async Task<string> GetDeviceId()
        {
            var deviceId = await SecureStorage.GetAsync(DeviceIdKey);
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString("N")[..16];
                await SecureStorage.SetAsync(DeviceIdKey, deviceId);
            }
            return deviceId;
        }

        public Task<string> GetDeviceName()
        {
            return Task.FromResult(DeviceInfo.Name);
        }

        public Task<int> GetDevicePlatform()
        {
            return Task.FromResult(DeviceInfo.Platform.ToInt());
        }

        public Task<int> GetDeviceType()
        {
            var type = DeviceInfo.DeviceType switch
            {
                DeviceType.Unknown => 0,
                DeviceType.Physical => 1,
                DeviceType.Virtual => 2,
                _ => 0
            };

            return Task.FromResult(type);
        }


    }
}
