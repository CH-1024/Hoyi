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
    public record UpdateUserReturn(bool IsSucc, string? Error = null);

    public interface IUserService
    {
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

        private readonly ISyncEventRepository _syncEventRepository;

        private readonly IUserRepository _userRepository;
        private readonly IContactRepository _contactRepository;

        public UserService(ISqlSugarClient db, ISyncEventRepository syncEventRepository, IUserRepository userRepository, IContactRepository contactRepository)
        {
            _db = db;

            _syncEventRepository = syncEventRepository;

            _userRepository = userRepository;
            _contactRepository = contactRepository;
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

            var result = await _db.Ado.UseTranAsync(async () =>
            {
                if (!await _userRepository.Update(userEntity)) throw new Exception("Failed to update user avatar.");

                var events = new List<SyncEventEntity>
                {
                    SyncEventFactory.Create(userId, SyncEventTypes.ProfileChanged, userId)
                };

                //var contacts = await _contactRepository.GetContactsByContactId(userId);
                //events.AddRange(contacts.Select(c => SyncEventFactory.Create(c.User_Id, SyncEventTypes.ContactsChanged, userId)));

                if (!await _syncEventRepository.InsertSyncEventsAsync(events)) throw new Exception("写入用户资料同步事件失败");
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
                if (!await _userRepository.Update(userEntity)) throw new Exception("Failed to update user profile.");

                var events = new List<SyncEventEntity>
                {
                    SyncEventFactory.Create(userId, SyncEventTypes.ProfileChanged, userId)
                };

                //var contacts = await _contactRepository.GetContactsByContactId(userId);
                //events.AddRange(contacts.Select(c => SyncEventFactory.Create(c.User_Id, SyncEventTypes.ContactsChanged, userId)));

                if (!await _syncEventRepository.InsertSyncEventsAsync(events)) throw new Exception("写入用户资料同步事件失败");
            });

            return new(result.IsSuccess, result.IsSuccess ? null : result.ErrorMessage);
        }
    }
}
