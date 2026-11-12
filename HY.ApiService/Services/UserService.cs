using HY.ApiService.Authorization;
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
        private readonly ISqlSugarClient _db;

        private readonly IUserRepository _userRepository;
        private readonly IPermissionRepository _permissionRepository;
        private readonly IContactRepository _contactRepository;
        private readonly ISyncEventRepository _syncEventRepository;

        public UserService(ISqlSugarClient db, IUserRepository userRepository, IPermissionRepository permissionRepository,
            IContactRepository contactRepository, ISyncEventRepository syncEventRepository)
        {
            _db = db;

            _userRepository = userRepository;
            _permissionRepository = permissionRepository;
            _contactRepository = contactRepository;
            _syncEventRepository = syncEventRepository;
        }




        public async Task<CreateUserReturn> CreateUser(RegisterUserRequest registerRequest)
        {
            if (await _userRepository.ExistsUsername(registerRequest.Username))
            {
                return new(false, "用户名已存在");
            }
            else if (await _userRepository.ExistsEmail(registerRequest.Email))
            {
                return new(false, "邮箱已存在");
            }
            else if (await _userRepository.ExistsPhone(registerRequest.Phone))
            {
                return new(false, "手机号已存在");
            }

            var hash = PBKDF2PasswordHasher.Hash(registerRequest.Password, out string salt);
            var hyid = Guid.NewGuid().ToString("N")[..16];

            long userId = 0;

            var result = await _db.Ado.UseTranAsync(async () =>
            {
                // 创建用户
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
                userId = await _userRepository.CreateUser(userEntity);

                if (userId <= 0) throw new Exception("创建用户失败");

                // 分配默认角色
                var assigned = await _permissionRepository.AssignRoleByCodeAsync(userId, RoleCodes.Guest);

                if (!assigned) throw new Exception("分配默认用户角色失败");
            });

            if (!result.IsSuccess)
            {
                return new(false, result.ErrorMessage);
            }

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
                return new(false, "User not found.");

            userEntity.Avatar = url;

            var result = await _db.Ado.UseTranAsync(async () =>
            {
                if (!await _userRepository.Update(userEntity))
                    throw new Exception("Failed to update user avatar.");

                await WriteProfileSyncEventsAsync(userId);
            });

            return new(result.IsSuccess, result.IsSuccess ? null : result.ErrorMessage);
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

            var result = await _db.Ado.UseTranAsync(async () =>
            {
                if (!await _userRepository.Update(userEntity))
                    throw new Exception("Failed to update user profile.");

                await WriteProfileSyncEventsAsync(userId);
            });

            return new(result.IsSuccess, result.IsSuccess ? null : result.ErrorMessage);
        }

        private async Task WriteProfileSyncEventsAsync(long userId)
        {
            var events = new List<SyncEventEntity>
            {
                SyncEventFactory.Create(userId, SyncEventTypes.ProfileChanged, userId)
            };

            var contacts = await _contactRepository.GetContactsByContactId(userId);
            events.AddRange(contacts.Select(c =>
                SyncEventFactory.Create(c.User_Id, SyncEventTypes.ContactsChanged, userId)));

            if (!await _syncEventRepository.InsertSyncEventsAsync(events))
                throw new Exception("写入用户资料同步事件失败");
        }
    }
}
