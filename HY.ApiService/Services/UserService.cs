using HY.ApiService.Controllers.Requests;
using HY.ApiService.Dtos;
using HY.ApiService.Entities;
using HY.ApiService.Enums;
using HY.ApiService.Repositories;
using HY.ApiService.Tools;
using Mapster;
using SqlSugar;

namespace HY.ApiService.Services
{
    public record CreateUserReturn(bool IsSucc, string? Error = null);
    public record UpdateUserReturn(bool IsSucc, string? Error = null);

    public interface IUserService
    {
        Task<CreateUserReturn> CreateUser(RegisterUserRequest registerRequest);

        Task<UserDto?> GetUserById(long id);

        Task<bool> ExistsUsername(string username);
        Task<bool> ExistsEmail(string email);
        Task<bool> ExistsPhone(string phone);

        Task<UpdateUserReturn> UpdateHead(long userId, string url);
        Task<UpdateUserReturn> UpdateProfile(long userId, UpdateUserRequest updateUserRequest);
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }




        public async Task<CreateUserReturn> CreateUser(RegisterUserRequest registerRequest)
        {
            if (await _userRepository.ExistsUsername(registerRequest.Username))
            {
                return new (false, "用户名已存在");
            }
            else if (await _userRepository.ExistsEmail(registerRequest.Email))
            {
                return new (false, "邮箱已存在");
            }
            else if (await _userRepository.ExistsPhone(registerRequest.Phone))
            {
                return new (false, "手机号已存在");
            }

            var hash = PBKDF2PasswordHasher.Hash(registerRequest.Password, out string salt);
            var hyid = Guid.NewGuid().ToString("N")[..16];

            var userEntity = new UserEntity
            {
                HYid = hyid,
                Username = registerRequest.Username,
                Nickname = registerRequest.Nickname,
                Avatar = null,
                Phone = registerRequest.Phone,
                Email = registerRequest.Email,
                Password_Hash = hash,
                Password_Salt = salt,
                Status = UserStatus.Registered,
                Created_At = DateTime.UtcNow
            };

            var userId = await _userRepository.CreateUser(userEntity);

            return new(userId > 0);
        }




        public async Task<UserDto?> GetUserById(long id)
        {
            var userEntity = await _userRepository.GetUserById(id);
            return userEntity?.Adapt<UserDto>();
        }




        public async Task<bool> ExistsUsername(string username)
        {
            return await _userRepository.ExistsUsername(username);
        }

        public async Task<bool> ExistsEmail(string email)
        {
            return await _userRepository.ExistsEmail(email);
        }

        public async Task<bool> ExistsPhone(string phone)
        {
            return await _userRepository.ExistsPhone(phone);
        }




        public async Task<UpdateUserReturn> UpdateHead(long userId, string url)
        {
            var userEntity = await _userRepository.GetUserById(userId);
            if (userEntity == null)
            {
                return new(false, "User not found.");
            }

            userEntity.Avatar = url;

            var isUpdated = await _userRepository.Update(userEntity);

            return new(isUpdated, isUpdated ? null : "Failed to update user avatar.");
        }

        public async Task<UpdateUserReturn> UpdateProfile(long userId, UpdateUserRequest updateUserRequest)
        {
            var userEntity = await _userRepository.GetUserById(userId);
            if (userEntity == null)
            {
                return new(false, "User not found.");
            }

            var emailExists = await _userRepository.ExistsEmail(updateUserRequest.Email);
            if (emailExists && userEntity.Email != updateUserRequest.Email)
            {
                return new(false, "Email already exists.");
            }

            var phoneExists = await _userRepository.ExistsPhone(updateUserRequest.Phone);
            if (phoneExists && userEntity.Phone != updateUserRequest.Phone)
            {
                return new(false, "Phone number already exists.");
            }

            userEntity.Nickname = updateUserRequest.Nickname ?? userEntity.Nickname;
            userEntity.Phone = updateUserRequest.Phone ?? userEntity.Phone;
            userEntity.Email = updateUserRequest.Email ?? userEntity.Email;

            var isUpdated = await _userRepository.Update(userEntity);

            return new(isUpdated, isUpdated ? null : "Failed to update user profile.");
        }
    }
}
