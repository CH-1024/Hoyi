using HY.MAUI.Communication.Http.Requests;
using HY.MAUI.Models;
using Makaretu.Dns;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;

namespace HY.MAUI.Communication.Http
{
    public class UserApi : BaseApi
    {
        public UserApi(HttpClient http) : base(http)
        {

        }



        public async Task<Response?> GetCurrentUser()
        {
            return await GetAsync(ApiUrl.GetCurrentUser);
        }

        public async Task<Response?> Register(string nickname, string username, string password, string email, string phone)
        {
            var param = new RegisterUserRequest
            {
                Nickname = nickname,
                Username = username,
                Password = password,
                Email = email,
                Phone = phone
            };

            return await PostAsJsonAsync(ApiUrl.Register, param);
        }

        public async Task<Response?> UpdateHead(string url)
        {
            return await PostAsync($"{ApiUrl.UpdateHead}?url={url}");
        }

        public async Task<Response?> UpdateProfile(string nickname, string email, string phone)
        {
            var param = new UpdateUserRequest
            {
                Nickname = nickname,
                Email = email,
                Phone = phone
            };
            return await PostAsJsonAsync(ApiUrl.UpdateProfile, param);
        }
    }
}
