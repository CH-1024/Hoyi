using HY.MAUI.Dtos;
using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Extensions.Mapping
{
    public static class UserMapping
    {
        public static UserVM ToVM(this UserDto dto)
        {
            return new UserVM
            {
                Id = dto.Id,
                HYid = dto.HYid,
                Nickname = dto.Nickname,
                Avatar = dto.Avatar,
                Phone = dto.Phone,
                Email = dto.Email,
                Region = dto.Region,
                Status = dto.Status,
                Created_At = dto.Created_At
            };
        }

    }
}
