using HY.ApiService.Controllers.Requests;
using HY.ApiService.Dtos;
using HY.ApiService.Entities;
using HY.ApiService.Enums;
using HY.ApiService.Models;
using HY.ApiService.Repositories;
using HY.ApiService.Tools;
using Mapster;
using SqlSugar;

namespace HY.ApiService.Services
{
    public record RefreshReturn(bool IsSucc, string? Error = null, UserDto? User = null, TokenModel? Token = null);
    public record LoginReturn(bool IsSucc, string? Error = null, UserDto? User = null, TokenModel? Token = null);

    public interface ILoginService
    {
        Task<RefreshReturn> Refresh(RefreshRequest param);
        Task<LoginReturn> Login(string ip, LoginRequest param);
        Task<bool> Logout(long userId, string deviceId);

        Task<bool> UpdateLoginDeviceOnline(long userId, string deviceId, bool isOnline);
    }


    public class LoginService : ILoginService
    {
        private readonly ISqlSugarClient _db;

        private readonly IRedisTokenService _redisTokenService;

        private readonly IUserRepository _userRepository;
        private readonly ILoginTokenRepository _loginTokenRepository;
        private readonly ILoginDeviceRepository _loginDeviceRepository;


        public LoginService(ISqlSugarClient db, IRedisTokenService redisTokenService, IUserRepository userRepository, ILoginTokenRepository loginTokenRepository, ILoginDeviceRepository loginDeviceRepository)
        {
            _db = db;

            _redisTokenService = redisTokenService;

            _userRepository = userRepository;
            _loginTokenRepository = loginTokenRepository;
            _loginDeviceRepository = loginDeviceRepository;
        }



        public async Task<RefreshReturn> Refresh(RefreshRequest param)
        {
            var loginTokenEntity = await _loginTokenRepository.GetValidityLoginTokenByDeviceIdAndRefreshToken(param.DeviceId, param.RefreshToken);
            if (loginTokenEntity == null)
            {
                return new (false, "无效的刷新请求!");
            }

            var user = await _userRepository.GetUserById(loginTokenEntity.User_Id);
            if (user == null)
            {
                return new (false, "用户不存在!");
            }
            else if (user.Status == UserStatus.Banned)
            {
                return new (false, "用户已被禁用!");
            }
            else if (user.Status == UserStatus.Deactivated)
            {
                return new (false, "用户已停用!");
            }
            else if (user.Status == UserStatus.Deleted)
            {
                return new (false, "用户已注销!");
            }

            var newRefreshToken = loginTokenEntity.Refresh_Token;

            if (loginTokenEntity.Refresh_Expired < DateTime.UtcNow.AddDays(7))
            {
                newRefreshToken = TokenGenerator.GenerateRefreshToken();

                loginTokenEntity.Refresh_Token = newRefreshToken;
                loginTokenEntity.Refresh_Expired = DateTime.UtcNow.AddDays(20);

                var bol = await _loginTokenRepository.UpdateLoginToken(loginTokenEntity);
                if (!bol) return new (false, "更新登录令牌失败!");
            }

            var newAccessToken = TokenGenerator.GenerateAccessToken(user.Id, param.DeviceId, param.DevicePlatform, out DateTime accessExpires);
            await _redisTokenService.SaveAsync(user.Id, param.DeviceId, newAccessToken, accessExpires);

            var token = new TokenModel()
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                AccessExpiresAt = accessExpires,
            };

            return new (true, null, user.Adapt<UserDto>(), token);
        }

        public async Task<LoginReturn> Login(string ip, LoginRequest param)
        {
            var user = await _userRepository.GetUserByUsername(param.Username);
            if (user == null)
            {
                return new (false, "用户名或密码错误!");
            }
            else if (user.Status == UserStatus.Banned)
            {
                return new (false, "用户已被禁用!");
            }
            else if (user.Status == UserStatus.Deactivated)
            {
                return new (false, "用户已停用!");
            }
            else if (user.Status == UserStatus.Deleted)
            {
                return new (false, "用户已注销!");
            }

            bool isValid = PBKDF2PasswordHasher.Verify(param.Password, user.Password_Hash!, user.Password_Salt!);
            if (!isValid)
            {
                return new (false, "用户名或密码错误!");
            }

            string accessToken = null;
            string refreshToken = null;
            DateTime accessExpires = default;

            // 开启事务
            var result = await _db.Ado.UseTranAsync(async () =>
            {
                #region LoginToken

                refreshToken = TokenGenerator.GenerateRefreshToken();

                var loginTokenEntity = await _loginTokenRepository.GetLoginTokenByUserIdAndDeviceId(user.Id, param.DeviceId);
                if (loginTokenEntity == null)
                {
                    loginTokenEntity = new LoginTokenEntity
                    {
                        User_Id = user.Id,
                        Device_Id = param.DeviceId,
                        Refresh_Token = refreshToken,
                        Refresh_Expired = DateTime.UtcNow.AddDays(30),
                        Revoked = false,
                        Created_At = DateTime.UtcNow,
                    };

                    var id = await _loginTokenRepository.CreateLoginToken(loginTokenEntity);
                    if (id <= 0) throw new Exception("创建登录令牌失败!");
                }
                else
                {
                    loginTokenEntity.Refresh_Token = refreshToken;
                    loginTokenEntity.Refresh_Expired = DateTime.UtcNow.AddDays(30);
                    loginTokenEntity.Revoked = false;

                    var bol = await _loginTokenRepository.UpdateLoginToken(loginTokenEntity);
                    if (!bol) throw new Exception("更新登录令牌失败!");
                }

                #endregion

                #region LoginDevice

                var device = await _loginDeviceRepository.GetLoginDeviceByUserIdAndDeviceId(user.Id, param.DeviceId);
                if (device == null)
                {
                    device = new LoginDeviceEntity
                    {
                        User_Id = user.Id,
                        Device_Id = param.DeviceId,
                        Device_Platform = param.DevicePlatform,
                        Device_Type = param.DeviceType,
                        Device_Name = param.DeviceName,
                        Last_Login_Ip = ip,
                        Last_Login_At = DateTime.UtcNow,
                        //Is_Online = true,                   // IsOnline只在ChatHub实时更新
                    };

                    var id = await _loginDeviceRepository.CreateLoginDevice(device);
                    if (id <= 0) throw new Exception("创建登录设备失败!");
                }
                else
                {
                    device.Device_Platform = param.DevicePlatform;
                    device.Device_Type = param.DeviceType;
                    device.Device_Name = param.DeviceName;
                    device.Last_Login_Ip = ip;
                    device.Last_Login_At = DateTime.UtcNow;
                    //device.Is_Online = true;       // IsOnline只在ChatHub实时更新

                    var bol = await _loginDeviceRepository.UpdateLoginDevice(device);
                    if (!bol) throw new Exception("更新登录设备失败!");
                }

                var devices = await _loginDeviceRepository.GetLoginDeviceByUserIdAndDevicePlatformAndIsOnline(user.Id, param.DevicePlatform);
                foreach (var d in devices)
                {
                    // Todo: 下线处理
                    d.Is_Online = false;
                    var bol = await _loginDeviceRepository.UpdateLoginDevice(d);
                    if (!bol) throw new Exception("更新登录设备状态失败!");
                }

                #endregion

                accessToken = TokenGenerator.GenerateAccessToken(user.Id, param.DeviceId, param.DevicePlatform, out accessExpires);
            });

            // ---------- 事务结束 ----------
            if (!result.IsSuccess)
            {
                return new (false, "登录失败：" + result.ErrorMessage);
            }

            // Redis 不放事务里
            await _redisTokenService.SaveAsync(user.Id, param.DeviceId, accessToken, accessExpires);

            var token = new TokenModel()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessExpiresAt = accessExpires,
            };

            return new (true, null, user.Adapt<UserDto>(), token);
        }

        public async Task<bool> Logout(long userId, string deviceId)
        {
            // 删除RedisTokenService中的Token
            await _redisTokenService.RemoveAsync(userId, deviceId);

            // 更新登录令牌状态
            return await _loginTokenRepository.UpdateLoginTokenRevoked(userId, deviceId);
        }

        public async Task<bool> UpdateLoginDeviceOnline(long userId, string deviceId, bool isOnline)
        {
            return await _loginDeviceRepository.UpdateLoginDeviceOnline(userId, deviceId, isOnline);
        }

    }
}
